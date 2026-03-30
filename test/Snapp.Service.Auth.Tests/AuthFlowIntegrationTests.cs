using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Auth;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Auth.Tests;

/// <summary>
/// End-to-end integration tests for the magic link authentication flow.
/// Tests call the API through Kong (http://localhost:8000) and verify emails via Papercut.
/// </summary>
[Collection(DockerTestCollection.Name)]
public class AuthFlowIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;

    private const string AuthBaseUrl = "/api/auth";
    private static string TestEmail => $"testuser-{Guid.NewGuid():N}@example.com";

    public AuthFlowIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _http = new HttpClient { BaseAddress = new Uri(fixture.KongUrl) };
        _dynamo = new DynamoDbTestHelper(fixture.DynamoDbUrl);
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await _fixture.PapercutClient.DeleteAllMessagesAsync();
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to complete the full magic link auth flow. Returns null if the auth
    /// service is not deployed (503/404 from Kong).
    /// </summary>
    private async Task<(TokenResponse Token, string Email)?> CompleteMagicLinkFlowAsync(string? email = null)
    {
        email ??= TestEmail;

        var requestResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = email });

        // If service isn't deployed, Kong returns 503 or 404
        if (requestResponse.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            return null;

        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/auth/magic-link should return 200");

        // Wait briefly for email delivery
        await Task.Delay(1000);

        var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
        code.Should().NotBeNullOrEmpty("magic link email should contain a code parameter");

        var validateResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/validate",
            new MagicLinkValidateRequest { Code = code! });

        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/auth/validate should return 200 with valid code");

        var token = await validateResponse.Content.ReadFromJsonAsync<TokenResponse>();
        token.Should().NotBeNull();
        token!.AccessToken.Should().NotBeNullOrEmpty();
        token.RefreshToken.Should().NotBeNullOrEmpty();
        token.ExpiresIn.Should().BeGreaterThan(0);

        return (token, email);
    }

    /// <summary>
    /// Checks if the auth service is reachable through Kong.
    /// Returns true if the service is deployed and responding.
    /// </summary>
    private async Task<bool> IsAuthServiceAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{AuthBaseUrl}/health");
            return response.StatusCode != HttpStatusCode.ServiceUnavailable
                && response.StatusCode != HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // Test 1: Full magic link flow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkFlow_ValidEmail_ReturnsJwtWithCorrectClaims()
    {
        // Arrange & Act
        var result = await CompleteMagicLinkFlowAsync();
        if (result is null)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        var (token, email) = result.Value;

        // Assert: decode JWT and verify claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);

        jwt.Subject.Should().NotBeNullOrEmpty("JWT must contain 'sub' claim");
        jwt.Issuer.Should().NotBeNullOrEmpty("JWT must contain 'iss' claim");
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow, "JWT must not be expired");

        // exp should be roughly AccessTokenTtlMinutes in the future
        var expectedExpiry = DateTime.UtcNow.AddMinutes(Limits.AccessTokenTtlMinutes);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(2));
    }

    // -----------------------------------------------------------------------
    // Test 2: Expired magic link
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkValidate_ExpiredCode_Returns401()
    {
        // Since we can't easily wait 15 minutes in a test, verify via DynamoDB TTL.
        // Create a magic link, then manually expire it in DynamoDB, then try to validate.
        var email = TestEmail;

        var requestResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = email });

        if (requestResponse.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);
        var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
        code.Should().NotBeNullOrEmpty();

        // Manually expire the token in DynamoDB by setting ExpiresAt to the past
        // ExpiresAt is stored as a Number (unix timestamp), not a String
        var pk = $"{KeyPrefixes.Token}{code}";
        var sk = SortKeyValues.MagicLink;
        var pastUnixSeconds = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString();

        await _dynamo.Client.UpdateItemAsync(new Amazon.DynamoDBv2.Model.UpdateItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                ["PK"] = new(pk),
                ["SK"] = new(sk)
            },
            UpdateExpression = "SET ExpiresAt = :exp",
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                [":exp"] = new() { N = pastUnixSeconds }
            }
        });

        // Act
        var validateResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/validate",
            new MagicLinkValidateRequest { Code = code! });

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "expired magic link should return 401");
    }

    // -----------------------------------------------------------------------
    // Test 3: Used magic link (single-use)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkValidate_UsedCode_Returns401OnSecondAttempt()
    {
        // Arrange: complete the flow once
        var email = TestEmail;
        var result = await CompleteMagicLinkFlowAsync(email);
        if (result is null)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        // We need the original code — extract it again from Papercut
        // (flow already consumed it, so we need to get it from the email that's still there)
        var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
        code.Should().NotBeNullOrEmpty("email should still be in Papercut");

        // Act: try to use the same code again
        var secondAttempt = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/validate",
            new MagicLinkValidateRequest { Code = code! });

        // Assert
        secondAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "magic link codes are single-use; second attempt should return 401");
    }

    // -----------------------------------------------------------------------
    // Test 4: Token refresh
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TokenRefresh_ValidRefreshToken_ReturnsNewTokenPair()
    {
        // Arrange
        var result = await CompleteMagicLinkFlowAsync();
        if (result is null)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        var originalRefreshToken = result.Value.Token.RefreshToken;

        // Act: refresh
        var refreshResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/refresh",
            new RefreshRequest { RefreshToken = originalRefreshToken });

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/auth/refresh should return 200 with valid refresh token");

        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        newTokens.Should().NotBeNull();
        newTokens!.AccessToken.Should().NotBeNullOrEmpty();
        newTokens.RefreshToken.Should().NotBeNullOrEmpty();
        newTokens.RefreshToken.Should().NotBe(originalRefreshToken,
            "refresh should issue a new refresh token (rotation)");

        // Act: try the old refresh token again
        var staleRefreshResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/refresh",
            new RefreshRequest { RefreshToken = originalRefreshToken });

        // Assert: old refresh token should be invalidated
        staleRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "old refresh token should be invalidated after rotation");
    }

    // -----------------------------------------------------------------------
    // Test 5: Logout
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Logout_ValidSession_InvalidatesRefreshToken()
    {
        // Arrange
        var result = await CompleteMagicLinkFlowAsync();
        if (result is null)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        var refreshToken = result.Value.Token.RefreshToken;

        // Act: logout
        var logoutResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/logout",
            new RefreshRequest { RefreshToken = refreshToken });

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/auth/logout should return 200");

        // Act: try to refresh after logout
        var refreshAfterLogout = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/refresh",
            new RefreshRequest { RefreshToken = refreshToken });

        // Assert
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "refresh token should be invalidated after logout");
    }

    // -----------------------------------------------------------------------
    // Test 6: Rate limiting
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLink_ExceedsRateLimit_Returns429()
    {
        // Arrange: use a single email to trigger rate limiting
        var email = TestEmail;
        var maxAllowed = Limits.MagicLinkRateLimitPerWindow; // 3

        // Act: send requests up to the limit — all should succeed
        for (var i = 0; i < maxAllowed; i++)
        {
            var response = await _http.PostAsJsonAsync(
                $"{AuthBaseUrl}/magic-link",
                new MagicLinkRequest { Email = email });

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            {
                Assert.Fail("Auth service not available — remove Skip once deployed");
                return;
            }

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"request {i + 1} of {maxAllowed} should succeed");
        }

        // Act: send one more — should be rate-limited
        var rateLimitedResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = email });

        // Assert
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            $"request {maxAllowed + 1} should return 429 (rate limit is {maxAllowed} per {Limits.RateLimitWindowMinutes}min window)");
    }

    // -----------------------------------------------------------------------
    // Test 7: Kong routes auth traffic correctly
    // -----------------------------------------------------------------------

    [Fact]
    public async Task KongRouting_AuthEndpoints_ReachAuthService()
    {
        // Verify Kong routes /api/auth/* to the auth service (not 503/404)
        var response = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = TestEmail });

        // Auth service should respond (not Kong's 503 for missing upstream)
        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable,
            "Kong should route /api/auth/* to the running auth service");
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "Kong should have a route for /api/auth/*");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "magic-link request should succeed through Kong");
    }

    // -----------------------------------------------------------------------
    // Test 8: Auto-creation of new user on first login
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkFlow_NewEmail_CreatesUserAndSetsIsNewUserTrue()
    {
        // Arrange: use a never-before-seen email
        var email = TestEmail;

        // Act
        var result = await CompleteMagicLinkFlowAsync(email);
        if (result is null)
        {
            Assert.Fail("Auth service not available — remove Skip once deployed");
            return;
        }

        var (token, _) = result.Value;

        // Assert: IsNewUser flag
        token.IsNewUser.Should().BeTrue(
            "first login with a new email should set IsNewUser = true");

        // Assert: verify user record exists in DynamoDB
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);
        var userId = jwt.Subject;
        userId.Should().NotBeNullOrEmpty();

        var userRecord = await _dynamo.Client.GetItemAsync(new Amazon.DynamoDBv2.Model.GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new("PROFILE")
            }
        });

        userRecord.Item.Should().NotBeNullOrEmpty(
            "user record should exist in DynamoDB after first login");
    }

    // -----------------------------------------------------------------------
    // Test 9: JWT contains correct claims
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkFlow_ValidAuth_JwtContainsRequiredClaims()
    {
        var result = await CompleteMagicLinkFlowAsync();
        if (result is null)
        {
            Assert.Fail("Auth service not available");
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Value.Token.AccessToken);

        // sub — user ID
        jwt.Subject.Should().NotBeNullOrEmpty("JWT must contain 'sub' claim with user ID");

        // iss — issuer
        jwt.Issuer.Should().NotBeNullOrEmpty("JWT must contain 'iss' claim");

        // aud — audience (if present, should not be empty)
        var audiences = jwt.Audiences.ToList();
        // Audience may or may not be required depending on config, but if set it should be valid
        if (audiences.Count > 0)
        {
            audiences.Should().AllSatisfy(a => a.Should().NotBeNullOrEmpty());
        }

        // exp — must be in the future
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow, "JWT 'exp' must be in the future");

        // exp should be roughly AccessTokenTtlMinutes from now
        var expectedExpiry = DateTime.UtcNow.AddMinutes(Limits.AccessTokenTtlMinutes);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(2),
            $"JWT 'exp' should be ~{Limits.AccessTokenTtlMinutes} minutes from now");
    }

    // -----------------------------------------------------------------------
    // Test 10: PII is encrypted in DynamoDB
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkFlow_NewUser_PiiIsEncryptedInDynamoDB()
    {
        var email = TestEmail;

        var result = await CompleteMagicLinkFlowAsync(email);
        if (result is null)
        {
            Assert.Fail("Auth service not available");
            return;
        }

        // Get the userId from the JWT
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Value.Token.AccessToken);
        var userId = jwt.Subject;

        // Read the raw PII item from DynamoDB
        var piiRecord = await _dynamo.Client.GetItemAsync(new Amazon.DynamoDBv2.Model.GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Pii)
            }
        });

        piiRecord.Item.Should().NotBeNullOrEmpty("PII record should exist for new user");

        // The EncryptedEmail field should NOT contain the plaintext email
        if (piiRecord.Item.TryGetValue("EncryptedEmail", out var encryptedField))
        {
            var storedValue = encryptedField.S;
            storedValue.Should().NotBeNullOrEmpty("EncryptedEmail field should have a value");
            storedValue.Should().NotContain(email.Split('@')[0],
                "stored email should be encrypted, not plaintext");
            storedValue.Should().NotContain("@example.com",
                "stored email should not contain plaintext domain");
        }

        // EncryptionKeyId should be present
        if (piiRecord.Item.TryGetValue("EncryptionKeyId", out var keyIdField))
        {
            keyIdField.S.Should().NotBeNullOrEmpty("EncryptionKeyId should be set");
        }
    }

    // -----------------------------------------------------------------------
    // Test 11: Magic link email contains valid URL with code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLink_EmailBody_ContainsValidUrlWithCode()
    {
        var email = TestEmail;

        var requestResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = email });

        if (requestResponse.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("Auth service not available");
            return;
        }

        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);

        // Get the raw email from Papercut
        var messages = await _fixture.PapercutClient.GetMessagesForRecipientAsync(email);
        messages.Should().NotBeEmpty("magic link email should be received by Papercut");

        var latest = messages.OrderByDescending(m => m.ReceivedAt).First();

        // Email body should contain a URL with the code parameter
        latest.Body.Should().Contain("code=",
            "email body should contain a magic link URL with a code parameter");

        // Extract and validate the URL format
        var urlMatch = System.Text.RegularExpressions.Regex.Match(
            latest.Body, @"(https?://[^\s""<]+code=[A-Za-z0-9_-]+)");
        urlMatch.Success.Should().BeTrue(
            "email body should contain a well-formed URL with the code parameter");

        // The code should be extractable via PapercutClient
        var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
        code.Should().NotBeNullOrEmpty("code should be extractable from the magic link email");
    }
}

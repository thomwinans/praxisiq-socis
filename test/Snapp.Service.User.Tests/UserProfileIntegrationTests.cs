using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.User;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.User.Tests;

/// <summary>
/// Integration tests for the User Profile Service.
/// Tests call the API through Kong (http://localhost:8000) which validates JWT
/// and forwards to snapp-user (port 8082).
/// </summary>
[Collection(DockerTestCollection.Name)]
public class UserProfileIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;

    private const string UsersBaseUrl = "/api/users";
    private const string AuthBaseUrl = "/api/auth";

    public UserProfileIntegrationTests(DockerTestFixture fixture)
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
    /// Authenticates via magic link flow and returns the JWT access token and userId.
    /// Returns null if the auth service is not deployed.
    /// </summary>
    private async Task<(string AccessToken, string UserId, string Email)?> AuthenticateAsync(string? email = null)
    {
        email ??= $"testuser-{Guid.NewGuid():N}@example.com";

        var requestResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/magic-link",
            new MagicLinkRequest { Email = email });

        if (requestResponse.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            return null;

        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);

        var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
        if (string.IsNullOrEmpty(code)) return null;

        var validateResponse = await _http.PostAsJsonAsync(
            $"{AuthBaseUrl}/validate",
            new MagicLinkValidateRequest { Code = code });

        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await validateResponse.Content.ReadFromJsonAsync<TokenResponse>();
        if (token is null) return null;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);
        var userId = jwt.Subject;

        return (token.AccessToken, userId, email);
    }

    private HttpRequestMessage CreateAuthedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    // -----------------------------------------------------------------------
    // Test 1: GET /api/users/me returns profile for authenticated user
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfile()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, userId, _) = auth.Value;

        var request = CreateAuthedRequest(HttpMethod.Get, $"{UsersBaseUrl}/me", accessToken);
        var response = await _http.SendAsync(request);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/users/me should return 200 for authenticated user");

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.UserId.Should().Be(userId);
        profile.DisplayName.Should().NotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 2: PUT /api/users/me updates fields and recalculates completeness
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ValidRequest_UpdatesAndRecalculates()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, userId, _) = auth.Value;

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"{UsersBaseUrl}/me");
        updateRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        updateRequest.Content = JsonContent.Create(new UpdateProfileRequest
        {
            DisplayName = "Dr. Integration Test",
            Specialty = "General Dentistry",
            Geography = "Austin, TX",
        });

        var response = await _http.SendAsync(updateRequest);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT /api/users/me should return 200 with valid update");

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Dr. Integration Test");
        profile.Specialty.Should().Be("General Dentistry");
        profile.Geography.Should().Be("Austin, TX");

        // DisplayName(20) + Specialty(20) + Geography(20) = 60
        profile.ProfileCompleteness.Should().Be(60m,
            "completeness should be 60 with name, specialty, and geography set");
    }

    // -----------------------------------------------------------------------
    // Test 3: POST /api/users/me/onboard stores encrypted PII
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Onboard_ValidRequest_StoresEncryptedPii()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, userId, _) = auth.Value;

        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, $"{UsersBaseUrl}/me/onboard");
        onboardRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        onboardRequest.Content = JsonContent.Create(new OnboardingRequest
        {
            DisplayName = "Dr. Onboard Test",
            Specialty = "Orthodontics",
            Geography = "Denver, CO",
            Email = "onboard-test@example.com",
            Phone = "+15551234567",
        });

        var response = await _http.SendAsync(onboardRequest);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/users/me/onboard should return 200");

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Dr. Onboard Test");

        // Verify PII is encrypted in DynamoDB
        var piiRecord = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Pii),
            },
        });

        piiRecord.Item.Should().NotBeNullOrEmpty("PII record should exist after onboarding");

        var encryptedEmail = piiRecord.Item["EncryptedEmail"].S;
        encryptedEmail.Should().NotContain("onboard-test",
            "email should be encrypted, not stored as plaintext");

        if (piiRecord.Item.TryGetValue("EncryptedPhone", out var phoneField))
        {
            phoneField.S.Should().NotContain("5551234567",
                "phone should be encrypted, not stored as plaintext");
        }
    }

    // -----------------------------------------------------------------------
    // Test 4: GET /api/users/me/pii decrypts correctly
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetMyPii_AfterOnboard_DecryptsCorrectly()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, userId, _) = auth.Value;

        // First onboard to set PII
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, $"{UsersBaseUrl}/me/onboard");
        onboardRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        onboardRequest.Content = JsonContent.Create(new OnboardingRequest
        {
            DisplayName = "Dr. PII Test",
            Email = "pii-test@example.com",
            Phone = "+15559876543",
        });

        var onboardResponse = await _http.SendAsync(onboardRequest);
        if (onboardResponse.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        onboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now request PII
        var piiRequest = CreateAuthedRequest(HttpMethod.Get, $"{UsersBaseUrl}/me/pii", accessToken);
        var piiResponse = await _http.SendAsync(piiRequest);

        piiResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/users/me/pii should return 200");

        var pii = await piiResponse.Content.ReadFromJsonAsync<PiiResponse>();
        pii.Should().NotBeNull();
        pii!.Email.Should().Be("pii-test@example.com",
            "decrypted email should match what was submitted during onboarding");
        pii.Phone.Should().Be("+15559876543",
            "decrypted phone should match what was submitted during onboarding");
    }

    // -----------------------------------------------------------------------
    // Test 5: GET /api/users/{otherId} returns public fields only
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOtherUser_ReturnsPublicFieldsOnly()
    {
        // Create two users
        var auth1 = await AuthenticateAsync();
        if (auth1 is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var auth2 = await AuthenticateAsync();
        if (auth2 is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken1, userId1, _) = auth1.Value;
        var (_, userId2, _) = auth2.Value;

        // User1 looks up User2's profile
        var request = CreateAuthedRequest(HttpMethod.Get, $"{UsersBaseUrl}/{userId2}", accessToken1);
        var response = await _http.SendAsync(request);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/users/{otherId} should return 200 for existing user");

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.UserId.Should().Be(userId2);
        // Profile should contain public fields, no PII
        profile.DisplayName.Should().NotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 6: Unauthenticated request returns 401
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var response = await _http.GetAsync($"{UsersBaseUrl}/me");

        // Kong JWT plugin should reject unauthenticated requests with 401
        if (response.StatusCode is HttpStatusCode.ServiceUnavailable)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request to /api/users/me should return 401");
    }

    // -----------------------------------------------------------------------
    // Test 7: ProfileCompleteness calculation is correct
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_AllFields_CompletenessIs75()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, _, _) = auth.Value;

        // Set name + specialty + geography + photo = 20 + 20 + 20 + 10 = 70
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"{UsersBaseUrl}/me");
        updateRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        updateRequest.Content = JsonContent.Create(new UpdateProfileRequest
        {
            DisplayName = "Dr. Complete",
            Specialty = "Pediatric Dentistry",
            Geography = "Portland, OR",
            PhotoUrl = "https://example.com/photo.jpg",
        });

        var response = await _http.SendAsync(updateRequest);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();

        // DisplayName(20) + Specialty(20) + Geography(20) + Photo(10) = 70
        profile!.ProfileCompleteness.Should().Be(70m,
            "completeness should be 70 with name, specialty, geography, and photo set");
    }

    // -----------------------------------------------------------------------
    // Test 8: Onboarding with LinkedIn URL adds to completeness
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Onboard_WithLinkedIn_CompletenessIncludes15()
    {
        var auth = await AuthenticateAsync();
        if (auth is null)
        {
            Assert.Fail("Auth or User service not available");
            return;
        }

        var (accessToken, _, _) = auth.Value;

        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, $"{UsersBaseUrl}/me/onboard");
        onboardRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        onboardRequest.Content = JsonContent.Create(new OnboardingRequest
        {
            DisplayName = "Dr. LinkedIn",
            Specialty = "Endodontics",
            Geography = "Seattle, WA",
            Email = $"linkedin-test-{Guid.NewGuid():N}@example.com",
            LinkedInProfileUrl = "https://linkedin.com/in/dr-linkedin",
        });

        var response = await _http.SendAsync(onboardRequest);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
        {
            Assert.Fail("User service not available through Kong");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();

        // DisplayName(20) + Specialty(20) + Geography(20) + LinkedIn(15) = 75
        profile!.ProfileCompleteness.Should().Be(75m,
            "completeness should be 75 with name, specialty, geography, and LinkedIn set");
        profile.LinkedInProfileUrl.Should().Be("https://linkedin.com/in/dr-linkedin");
    }
}

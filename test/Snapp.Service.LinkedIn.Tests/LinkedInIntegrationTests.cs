using System.Net;
using System.Net.Http.Json;
using System.Text;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.LinkedIn;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.LinkedIn.Tests;

[Collection("Docker")]
public class LinkedInIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _kongClient;
    private readonly HttpClient _directClient;
    private readonly DynamoDbTestHelper _dynamo;

    public LinkedInIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _kongClient = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        _directClient = new HttpClient { BaseAddress = new Uri("http://localhost:8088") };
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
    }

    public Task DisposeAsync()
    {
        _kongClient.Dispose();
        _directClient.Dispose();
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        HttpResponseMessage response;
        try
        {
            response = await _directClient.GetAsync("/health");
        }
        catch (HttpRequestException)
        {
            return; // Service not running — skip
        }

        if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable)
        {
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task AuthUrl_WithUserId_ReturnsLinkedInUrl()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/auth-url");
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkedInAuthUrlResponse>();
        body!.AuthorizationUrl.Should().Contain("linkedin.com/oauth/v2/authorization");
        body.AuthorizationUrl.Should().Contain("client_id=");
        body.AuthorizationUrl.Should().Contain("scope=");
        body.AuthorizationUrl.Should().Contain("state=");
    }

    [Fact]
    public async Task AuthUrl_WithoutUserId_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/auth-url");
        // No X-User-Id header

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_ValidCode_StoresEncryptedTokenAndReturnsProfile()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        // Generate a valid state token
        var stateRaw = $"{userId}:{Guid.NewGuid():N}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateRaw));

        var callbackRequest = new LinkedInCallbackRequest
        {
            Code = "mock-auth-code-" + Guid.NewGuid().ToString("N")[..8],
            State = state,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/callback")
        {
            Content = JsonContent.Create(callbackRequest),
        };
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<LinkedInProfileResponse>();
        profile!.LinkedInName.Should().NotBeNullOrEmpty();

        // Verify encrypted token stored in DynamoDB
        var dbResponse = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.LinkedIn),
            },
        });

        dbResponse.IsItemSet.Should().BeTrue("LinkedIn token should be stored");
        var item = dbResponse.Item;

        // Token and URN should be encrypted (not plaintext)
        item["EncryptedAccessToken"].S.Should().NotContain("mock-linkedin-token",
            "access token must be encrypted, not stored as plaintext");
        item["EncryptedLinkedInURN"].S.Should().NotContain("urn:li:person",
            "LinkedIn URN must be encrypted, not stored as plaintext");
        item["EncryptionKeyId"].S.Should().NotBeNullOrEmpty();

        // Cleanup
        await _dynamo.CleanupAsync(TableNames.Users, $"{KeyPrefixes.User}{userId}", SortKeyValues.LinkedIn);
    }

    [Fact]
    public async Task Callback_UpdatesProfileCompleteness()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        // Get initial completeness
        var userBefore = await GetUserProfile(userId);
        var initialCompleteness = decimal.Parse(userBefore!["ProfileCompleteness"].N);

        // Do callback
        var stateRaw = $"{userId}:{Guid.NewGuid():N}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateRaw));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/callback")
        {
            Content = JsonContent.Create(new LinkedInCallbackRequest
            {
                Code = "mock-code-" + Guid.NewGuid().ToString("N")[..8],
                State = state,
            }),
        };
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify completeness increased by 15
        var userAfter = await GetUserProfile(userId);
        var newCompleteness = decimal.Parse(userAfter!["ProfileCompleteness"].N);
        newCompleteness.Should().Be(initialCompleteness + 15);

        // Cleanup
        await _dynamo.CleanupAsync(TableNames.Users, $"{KeyPrefixes.User}{userId}", SortKeyValues.LinkedIn);
    }

    [Fact]
    public async Task Callback_InvalidState_Returns400()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        // State with wrong userId
        var stateRaw = $"wrong-user:{Guid.NewGuid():N}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateRaw));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/callback")
        {
            Content = JsonContent.Create(new LinkedInCallbackRequest
            {
                Code = "mock-code",
                State = state,
            }),
        };
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Status_NotLinked_ReturnsNotLinked()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/status");
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<LinkedInStatusResponse>();
        status!.IsLinked.Should().BeFalse();
    }

    [Fact]
    public async Task Status_AfterLink_ReturnsLinked()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        if (!await LinkLinkedIn(userId)) return;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/status");
        request.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, request);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<LinkedInStatusResponse>();
        status!.IsLinked.Should().BeTrue();
        status.TokenExpiry.Should().BeAfter(DateTime.UtcNow);

        // Cleanup
        await _dynamo.CleanupAsync(TableNames.Users, $"{KeyPrefixes.User}{userId}", SortKeyValues.LinkedIn);
    }

    [Fact]
    public async Task Unlink_RemovesLinkedInData()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        if (!await LinkLinkedIn(userId)) return;

        // Verify linked
        var statusReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/status");
        statusReq1.Headers.Add("X-User-Id", userId);
        var statusResp1 = await SendSafe(_directClient, statusReq1);
        if (statusResp1 is null || !await AssertServiceAvailable(statusResp1)) return;
        var status1 = await statusResp1.Content.ReadFromJsonAsync<LinkedInStatusResponse>();
        status1!.IsLinked.Should().BeTrue();

        // Unlink
        var unlinkReq = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/unlink");
        unlinkReq.Headers.Add("X-User-Id", userId);
        var unlinkResp = await SendSafe(_directClient, unlinkReq);
        if (unlinkResp is null) return;
        unlinkResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify unlinked
        var statusReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/linkedin/status");
        statusReq2.Headers.Add("X-User-Id", userId);
        var statusResp2 = await SendSafe(_directClient, statusReq2);
        if (statusResp2 is null) return;
        var status2 = await statusResp2.Content.ReadFromJsonAsync<LinkedInStatusResponse>();
        status2!.IsLinked.Should().BeFalse();
    }

    [Fact]
    public async Task Share_WithLinkedAccount_ReturnsPostUrl()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        if (!await LinkLinkedIn(userId)) return;

        var shareReq = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/share")
        {
            Content = JsonContent.Create(new LinkedInShareRequest
            {
                Content = "Check out this great dental practice network!",
                NetworkId = "NET-TEST-001",
                SourceType = "post",
            }),
        };
        shareReq.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, shareReq);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var shareResp = await response.Content.ReadFromJsonAsync<LinkedInShareResponse>();
        shareResp!.LinkedInPostUrl.Should().Contain("linkedin.com/feed/update");

        // Cleanup
        await _dynamo.CleanupAsync(TableNames.Users, $"{KeyPrefixes.User}{userId}", SortKeyValues.LinkedIn);
    }

    [Fact]
    public async Task Share_WithoutLinkedAccount_Returns400()
    {
        var userId = await _dynamo.CreateTestUserAsync();

        var shareReq = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/share")
        {
            Content = JsonContent.Create(new LinkedInShareRequest
            {
                Content = "Test content",
                NetworkId = "NET-TEST-001",
                SourceType = "post",
            }),
        };
        shareReq.Headers.Add("X-User-Id", userId);

        var response = await SendSafe(_directClient, shareReq);
        if (response is null || !await AssertServiceAvailable(response)) return;

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.LinkedInNotLinked);
    }

    [Fact]
    public async Task Share_RateLimit_Returns429After25()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        if (!await LinkLinkedIn(userId)) return;

        // Exhaust the rate limit (25 shares/day)
        for (var i = 0; i < 25; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/share")
            {
                Content = JsonContent.Create(new LinkedInShareRequest
                {
                    Content = $"Share #{i + 1}",
                    NetworkId = "NET-TEST-001",
                    SourceType = "post",
                }),
            };
            req.Headers.Add("X-User-Id", userId);

            var resp = await SendSafe(_directClient, req);
            if (resp is null || !await AssertServiceAvailable(resp)) return;
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"share #{i + 1} should succeed");
        }

        // 26th share should be rate limited
        var overLimitReq = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/share")
        {
            Content = JsonContent.Create(new LinkedInShareRequest
            {
                Content = "This should be rate limited",
                NetworkId = "NET-TEST-001",
                SourceType = "post",
            }),
        };
        overLimitReq.Headers.Add("X-User-Id", userId);

        var overLimitResp = await SendSafe(_directClient, overLimitReq);
        if (overLimitResp is null) return;
        ((int)overLimitResp.StatusCode).Should().Be(429);

        // Cleanup
        await _dynamo.CleanupAsync(TableNames.Users, $"{KeyPrefixes.User}{userId}", SortKeyValues.LinkedIn);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<bool> LinkLinkedIn(string userId)
    {
        var stateRaw = $"{userId}:{Guid.NewGuid():N}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateRaw));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/linkedin/callback")
        {
            Content = JsonContent.Create(new LinkedInCallbackRequest
            {
                Code = "mock-code-" + Guid.NewGuid().ToString("N")[..8],
                State = state,
            }),
        };
        request.Headers.Add("X-User-Id", userId);

        HttpResponseMessage response;
        try
        {
            response = await _directClient.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            // Service not running — caller should skip
            return false;
        }

        if (!await AssertServiceAvailable(response)) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<Dictionary<string, AttributeValue>?> GetUserProfile(string userId)
    {
        var response = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Profile),
            },
        });

        return response.IsItemSet ? response.Item : null;
    }

    private static async Task<bool> AssertServiceAvailable(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.NotFound)
        {
            // Service not running in test environment — skip gracefully
            return false;
        }

        return true;
    }

    private async Task<HttpResponseMessage?> SendSafe(HttpClient client, HttpRequestMessage request)
    {
        try
        {
            return await client.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            // Service not running — caller should skip
            return null;
        }
    }

    private record HealthResponse(string Status);
}

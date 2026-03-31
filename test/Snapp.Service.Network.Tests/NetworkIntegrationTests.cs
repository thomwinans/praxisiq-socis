using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Network;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Network.Tests;

[Collection(DockerTestCollection.Name)]
public class NetworkIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NetworkIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _http = new HttpClient { BaseAddress = new Uri(fixture.KongUrl), Timeout = TimeSpan.FromSeconds(15) };
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureNetworksTableAsync();
        // Do NOT clear Papercut inbox here — other test assemblies may be running
        // in parallel. Each test uses a unique UUID email, so recipient filtering
        // in PapercutClient.GetMessagesForRecipientAsync provides isolation.
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    // ── Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateNetwork_Authenticated_ReturnsCreatedWithStewardRole()
    {
        var jwt = await AuthenticateAsync($"net-create-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return; // Services not available

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest
        {
            Name = "Test Network",
            Description = "A test network",
            Charter = "Be excellent to each other",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Test Network");
        body.Description.Should().Be("A test network");
        body.MemberCount.Should().Be(1);
        body.UserRole.Should().Be("steward");
        body.NetworkId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateNetwork_DefaultRolesCreated()
    {
        var jwt = await AuthenticateAsync($"net-roles-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Roles Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = body!.NetworkId;

        // Verify roles in DynamoDB
        var stewardRole = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new("ROLE#steward"),
            },
        });
        stewardRole.IsItemSet.Should().BeTrue();

        var memberRole = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new("ROLE#member"),
            },
        });
        memberRole.IsItemSet.Should().BeTrue();

        var associateRole = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new("ROLE#associate"),
            },
        });
        associateRole.IsItemSet.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_SubmitsApplicationInPendingQueue()
    {
        var stewardJwt = await AuthenticateAsync($"net-apply-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network as steward
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Apply Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Apply as different user
        var applicantJwt = await AuthenticateAsync($"net-apply-user-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantJwt!);

        var applyResp = await _http.PostAsJsonAsync($"/api/networks/{netId}/apply", new ApplyRequest
        {
            NetworkId = netId,
            ApplicationText = "I'd love to join!",
        });

        applyResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify application in DynamoDB
        var applicantUserId = ExtractUserId(applicantJwt!);
        var appItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new($"APP#{applicantUserId}"),
            },
        });
        appItem.IsItemSet.Should().BeTrue();
        appItem.Item["Status"].S.Should().Be("Pending");
    }

    [Fact]
    public async Task Approve_UserBecomesMember_CountIncremented()
    {
        var stewardJwt = await AuthenticateAsync($"net-approve-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Approve Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Apply as different user
        var applicantJwt = await AuthenticateAsync($"net-approve-user-{Guid.NewGuid():N}@test.snapp");
        var applicantUserId = ExtractUserId(applicantJwt!);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantJwt!);

        await _http.PostAsJsonAsync($"/api/networks/{netId}/apply", new ApplyRequest
        {
            NetworkId = netId,
            ApplicationText = "Please approve me",
        });

        // Approve as steward
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var decideResp = await _http.PostAsJsonAsync($"/api/networks/{netId}/applications/{applicantUserId}/decide",
            new ApplicationDecisionRequest { UserId = applicantUserId, Decision = "Approved" });

        decideResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is now a member
        var memberItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new($"MEMBER#{applicantUserId}"),
            },
        });
        memberItem.IsItemSet.Should().BeTrue();
        memberItem.Item["Role"].S.Should().Be("member");

        // Verify count incremented (was 1 from creator, now 2)
        var netItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
        });
        int.Parse(netItem.Item["MemberCount"].N).Should().Be(2);
    }

    [Fact]
    public async Task Deny_UserNotAdded()
    {
        var stewardJwt = await AuthenticateAsync($"net-deny-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Deny Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Apply as different user
        var applicantJwt = await AuthenticateAsync($"net-deny-user-{Guid.NewGuid():N}@test.snapp");
        var applicantUserId = ExtractUserId(applicantJwt!);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantJwt!);

        await _http.PostAsJsonAsync($"/api/networks/{netId}/apply", new ApplyRequest
        {
            NetworkId = netId,
        });

        // Deny as steward
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var decideResp = await _http.PostAsJsonAsync($"/api/networks/{netId}/applications/{applicantUserId}/decide",
            new ApplicationDecisionRequest { UserId = applicantUserId, Decision = "Denied", Reason = "Not a fit" });

        decideResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is NOT a member
        var memberItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new($"MEMBER#{applicantUserId}"),
            },
        });
        memberItem.IsItemSet.Should().BeFalse();

        // Count should still be 1
        var netItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
        });
        int.Parse(netItem.Item["MemberCount"].N).Should().Be(1);
    }

    [Fact]
    public async Task NonSteward_CannotApproveOrDeny()
    {
        var stewardJwt = await AuthenticateAsync($"net-noperm-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Permission Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Apply as user A
        var applicantJwt = await AuthenticateAsync($"net-noperm-applicant-{Guid.NewGuid():N}@test.snapp");
        var applicantUserId = ExtractUserId(applicantJwt!);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantJwt!);

        await _http.PostAsJsonAsync($"/api/networks/{netId}/apply", new ApplyRequest { NetworkId = netId });

        // Try to approve as user B (non-steward, non-member)
        var randomJwt = await AuthenticateAsync($"net-noperm-random-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", randomJwt!);

        var decideResp = await _http.PostAsJsonAsync($"/api/networks/{netId}/applications/{applicantUserId}/decide",
            new ApplicationDecisionRequest { UserId = applicantUserId, Decision = "Approved" });

        decideResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMember_CannotSeeMembers()
    {
        var stewardJwt = await AuthenticateAsync($"net-nomem-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Members Visibility Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Try to list members as non-member
        var nonMemberJwt = await AuthenticateAsync($"net-nomem-outsider-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nonMemberJwt!);

        var membersResp = await _http.GetAsync($"/api/networks/{netId}/members");
        membersResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invite_BypassesApplication()
    {
        var stewardJwt = await AuthenticateAsync($"net-invite-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Invite Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Create a user to invite (authenticate to get userId)
        var inviteeJwt = await AuthenticateAsync($"net-invite-user-{Guid.NewGuid():N}@test.snapp");
        var inviteeUserId = ExtractUserId(inviteeJwt!);

        // Invite as steward
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var inviteResp = await _http.PostAsJsonAsync($"/api/networks/{netId}/invite", new { UserId = inviteeUserId });

        inviteResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify user is now a member
        var memberItem = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{netId}"),
                ["SK"] = new($"MEMBER#{inviteeUserId}"),
            },
        });
        memberItem.IsItemSet.Should().BeTrue();
        memberItem.Item["Role"].S.Should().Be("member");
    }

    [Fact]
    public async Task Mine_ReturnsOnlyUserNetworks()
    {
        var userJwt = await AuthenticateAsync($"net-mine-{Guid.NewGuid():N}@test.snapp");
        if (userJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userJwt);

        // Create two networks
        await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "My Net 1" });
        await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "My Net 2" });

        // Create a network as a different user
        var otherJwt = await AuthenticateAsync($"net-mine-other-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherJwt!);
        await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Other Net" });

        // List my networks
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userJwt);
        var mineResp = await _http.GetAsync("/api/networks/mine");
        mineResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await mineResp.Content.ReadFromJsonAsync<NetworkListResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Networks.Count.Should().Be(2);
        body.Networks.Should().OnlyContain(n => n.Name.StartsWith("My Net"));
    }

    [Fact]
    public async Task GetSettings_Steward_ReturnsSettingsWithRolesAndPendingCount()
    {
        var stewardJwt = await AuthenticateAsync($"net-settings-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest
        {
            Name = "Settings Test",
            Description = "Test desc",
            Charter = "Test charter",
        });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Add a pending application
        var applicantJwt = await AuthenticateAsync($"net-settings-applicant-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantJwt!);
        await _http.PostAsJsonAsync($"/api/networks/{netId}/apply", new ApplyRequest
        {
            NetworkId = netId,
            ApplicationText = "Let me in",
        });

        // Fetch settings as steward
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var settingsResp = await _http.GetAsync($"/api/networks/{netId}/settings");

        settingsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await settingsResp.Content.ReadFromJsonAsync<NetworkSettingsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Network.NetworkId.Should().Be(netId);
        body.Network.Name.Should().Be("Settings Test");
        body.Network.Charter.Should().Be("Test charter");
        body.Network.UserRole.Should().Be("steward");
        body.Roles.Should().HaveCount(3);
        body.Roles.Should().Contain(r => r.RoleName == "steward");
        body.Roles.Should().Contain(r => r.RoleName == "member");
        body.Roles.Should().Contain(r => r.RoleName == "associate");
        body.PendingApplicationCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSettings_NonStewardMember_ReturnsForbidden()
    {
        var stewardJwt = await AuthenticateAsync($"net-settings-ns-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Settings Perm Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Invite a member (non-steward)
        var memberJwt = await AuthenticateAsync($"net-settings-ns-member-{Guid.NewGuid():N}@test.snapp");
        var memberUserId = ExtractUserId(memberJwt!);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        await _http.PostAsJsonAsync($"/api/networks/{netId}/invite", new { UserId = memberUserId });

        // Try to get settings as member
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberJwt!);
        var settingsResp = await _http.GetAsync($"/api/networks/{netId}/settings");

        settingsResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSettings_NonMember_ReturnsForbidden()
    {
        var stewardJwt = await AuthenticateAsync($"net-settings-nm-steward-{Guid.NewGuid():N}@test.snapp");
        if (stewardJwt is null) return;

        // Create network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        var createResp = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest { Name = "Settings NonMember Test" });
        var network = await createResp.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        var netId = network!.NetworkId;

        // Try to get settings as non-member
        var outsiderJwt = await AuthenticateAsync($"net-settings-nm-outsider-{Guid.NewGuid():N}@test.snapp");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderJwt!);
        var settingsResp = await _http.GetAsync($"/api/networks/{netId}/settings");

        settingsResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<string?> AuthenticateAsync(string email)
    {
        try
        {
            // Request magic link, retry on 429 (rate limit during parallel tests)
            HttpResponseMessage magicResp;
            for (var attempt = 0; ; attempt++)
            {
                magicResp = await _http.PostAsJsonAsync("/api/auth/magic-link", new { Email = email });
                if (magicResp.StatusCode != HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;
                await Task.Delay(1000 * (attempt + 1));
            }
            if (!magicResp.IsSuccessStatusCode) return null;

            // Extract code from Papercut (polls with retries internally)
            var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
            if (code is null) return null;

            // Validate and get JWT, retry on 429
            HttpResponseMessage validateResp;
            for (var attempt = 0; ; attempt++)
            {
                validateResp = await _http.PostAsJsonAsync("/api/auth/validate", new { Code = code });
                if (validateResp.StatusCode != HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;
                await Task.Delay(1000 * (attempt + 1));
            }
            if (!validateResp.IsSuccessStatusCode) return null;

            var tokenBody = await validateResp.Content.ReadFromJsonAsync<JsonElement>();
            return tokenBody.GetProperty("accessToken").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractUserId(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Subject;
    }

    private async Task EnsureNetworksTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Networks);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Networks,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1PK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1SK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI2PK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI2SK", ScalarAttributeType.S),
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = GsiNames.UserNetworks,
                        KeySchema =
                        [
                            new KeySchemaElement("GSI1PK", KeyType.HASH),
                            new KeySchemaElement("GSI1SK", KeyType.RANGE),
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                    new GlobalSecondaryIndex
                    {
                        IndexName = GsiNames.PendingApps,
                        KeySchema =
                        [
                            new KeySchemaElement("GSI2PK", KeyType.HASH),
                            new KeySchemaElement("GSI2SK", KeyType.RANGE),
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }
}

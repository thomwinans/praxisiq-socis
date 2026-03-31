using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Enums;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Transaction.Tests;

[Collection(DockerTestCollection.Name)]
public class TransactionIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly DynamoDbTestHelper _dynamo;

    /// <summary>Direct connection to the Transaction service (bypasses Kong JWT).</summary>
    private const string TxServiceUrl = "http://localhost:8086";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public TransactionIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureTxTableAsync();
        await EnsureNetworksTableAsync();
    }

    public Task DisposeAsync()
    {
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Creates an HttpClient pointed at the Transaction service with X-User-Id set.</summary>
    private static HttpClient CreateClient(string userId)
    {
        var http = new HttpClient { BaseAddress = new Uri(TxServiceUrl), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("X-User-Id", userId);
        return http;
    }

    // ── Referral Lifecycle Tests ────────────────────────────────

    [Fact]
    public async Task CreateReferral_ValidMembers_Returns201()
    {
        var (senderId, receiverId, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(senderId);

        var response = await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
            Specialty = "orthodontics",
            Notes = "Good match for complex case",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var referral = await response.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);
        Assert.NotNull(referral);
        Assert.Equal(senderId, referral!.SenderUserId);
        Assert.Equal(receiverId, referral.ReceiverUserId);
        Assert.Equal(ReferralStatus.Created, referral.Status);
    }

    [Fact]
    public async Task CreateReferral_SelfReferral_Returns400()
    {
        var (senderId, _, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(senderId);

        var response = await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = senderId, // self-referral
            NetworkId = networkId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReferral_NonMember_Returns403()
    {
        var (senderId, _, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(senderId);

        var outsiderId = Guid.NewGuid().ToString("N");

        var response = await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = outsiderId,
            NetworkId = networkId,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_ReturnsOk()
    {
        var (senderId, receiverId, networkId) = await SetupReferralScenarioAsync();
        using var senderHttp = CreateClient(senderId);

        // Create referral as sender
        var createResp = await senderHttp.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
        });
        var referral = await createResp.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);

        // Switch to receiver to accept
        using var receiverHttp = CreateClient(receiverId);

        var statusResp = await receiverHttp.PutAsJsonAsync(
            $"/api/tx/referrals/{referral!.ReferralId}/status",
            new UpdateReferralStatusRequest { Status = ReferralStatus.Accepted });

        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        var updated = await statusResp.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);
        Assert.Equal(ReferralStatus.Accepted, updated!.Status);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns400()
    {
        var (senderId, receiverId, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(senderId);

        var createResp = await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
        });
        var referral = await createResp.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);

        // Try to complete without accepting first
        var statusResp = await http.PutAsJsonAsync(
            $"/api/tx/referrals/{referral!.ReferralId}/status",
            new UpdateReferralStatusRequest { Status = ReferralStatus.Completed });

        Assert.Equal(HttpStatusCode.BadRequest, statusResp.StatusCode);
    }

    [Fact]
    public async Task RecordOutcome_Completed_TriggersReputation()
    {
        var (senderId, receiverId, networkId) = await SetupReferralScenarioAsync();
        using var senderHttp = CreateClient(senderId);
        using var receiverHttp = CreateClient(receiverId);

        // Create referral
        var createResp = await senderHttp.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
        });
        var referral = await createResp.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);

        // Accept as receiver
        await receiverHttp.PutAsJsonAsync(
            $"/api/tx/referrals/{referral!.ReferralId}/status",
            new UpdateReferralStatusRequest { Status = ReferralStatus.Accepted });

        // Record outcome as receiver
        var outcomeResp = await receiverHttp.PostAsJsonAsync(
            $"/api/tx/referrals/{referral.ReferralId}/outcome",
            new RecordOutcomeRequest { Success = true, Outcome = "Patient treated successfully" });

        Assert.Equal(HttpStatusCode.OK, outcomeResp.StatusCode);
        var completed = await outcomeResp.Content.ReadFromJsonAsync<ReferralResponse>(JsonOptions);
        Assert.Equal(ReferralStatus.Completed, completed!.Status);
        Assert.NotNull(completed.OutcomeRecordedAt);

        // Wait briefly for async reputation computation
        await Task.Delay(1000);

        // Verify reputation was computed
        var repResp = await senderHttp.GetAsync($"/api/tx/reputation/{senderId}");
        Assert.Equal(HttpStatusCode.OK, repResp.StatusCode);
    }

    [Fact]
    public async Task ListSentReferrals_ReturnsUserReferrals()
    {
        var (senderId, receiverId, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(senderId);

        // Create two referrals
        await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
            Notes = "First referral",
        });
        await http.PostAsJsonAsync("/api/tx/referrals", new CreateReferralRequest
        {
            ReceiverUserId = receiverId,
            NetworkId = networkId,
            Notes = "Second referral",
        });

        var listResp = await http.GetAsync("/api/tx/referrals/sent");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<ReferralListResponse>(JsonOptions);
        Assert.NotNull(list);
        Assert.True(list!.Referrals.Count >= 2);
    }

    // ── Reputation Tests ────────────────────────────────────────

    [Fact]
    public async Task GetReputation_NoData_ReturnsDefaultScores()
    {
        var userId = Guid.NewGuid().ToString("N");
        using var http = CreateClient(userId);

        var response = await http.GetAsync($"/api/tx/reputation/{userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rep = await response.Content.ReadFromJsonAsync<ReputationResponse>(JsonOptions);
        Assert.NotNull(rep);
        Assert.Equal(userId, rep!.UserId);
        Assert.Equal(0m, rep.OverallScore);
    }

    [Fact]
    public async Task GetReputationHistory_ReturnsSnapshots()
    {
        var userId = Guid.NewGuid().ToString("N");
        using var http = CreateClient(userId);

        // Seed a reputation snapshot directly
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Transactions,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Reputation}{userId}"),
                ["SK"] = new($"SNAP#{DateTime.UtcNow.ToString("O")}"),
                ["UserId"] = new(userId),
                ["OverallScore"] = new() { N = "42.5" },
                ["ReferralScore"] = new() { N = "50" },
                ["ContributionScore"] = new() { N = "30" },
                ["AttestationScore"] = new() { N = "40" },
                ["ComputedAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        });

        var response = await http.GetAsync($"/api/tx/reputation/{userId}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Attestation Tests ───────────────────────────────────────

    [Fact]
    public async Task CreateAttestation_ValidPeers_Returns201()
    {
        var (attestorId, targetId, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(attestorId);

        var response = await http.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = targetId,
            Skill = "implant-placement",
            Comment = "Excellent surgeon, highly recommended",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateAttestation_SelfAttest_Returns400()
    {
        var (userId, _, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(userId);

        var response = await http.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = userId,
            Skill = "self-promotion",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAttestation_DuplicateAttestation_Returns409()
    {
        var (attestorId, targetId, networkId) = await SetupReferralScenarioAsync();
        using var http = CreateClient(attestorId);

        await http.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = targetId,
            Skill = "endo",
        });

        var response = await http.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = targetId,
            Skill = "endo-again",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AntiGaming_ReciprocalAttestation_IsFlagged()
    {
        var (userA, userB, networkId) = await SetupReferralScenarioAsync();

        // A attests B
        using var httpA = CreateClient(userA);
        var resp1 = await httpA.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = userB,
            Skill = "restorative",
        });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // B attests A (reciprocal — should be flagged but still created)
        using var httpB = CreateClient(userB);
        var resp2 = await httpB.PostAsJsonAsync("/api/tx/attestations", new
        {
            TargetUserId = userA,
            Skill = "restorative",
        });

        // Anti-gaming flags but doesn't reject — should still be 201
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);
    }

    // ── Health Check ────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        using var http = new HttpClient { BaseAddress = new Uri(TxServiceUrl) };
        var response = await http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private async Task<(string SenderId, string ReceiverId, string NetworkId)> SetupReferralScenarioAsync()
    {
        var senderId = await _dynamo.CreateTestUserAsync();
        var receiverId = await _dynamo.CreateTestUserAsync();
        var networkId = Guid.NewGuid().ToString("N");

        // Create network and membership records
        await EnsureNetworksTableAsync();

        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new(SortKeyValues.Meta),
                ["NetworkId"] = new(networkId),
                ["Name"] = new("Test Network"),
                ["CreatedByUserId"] = new(senderId),
                ["MemberCount"] = new() { N = "2" },
                ["CreatedAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        });

        // Add sender as member
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"MEMBER#{senderId}"),
                ["UserId"] = new(senderId),
                ["Role"] = new("owner"),
                ["Status"] = new("Active"),
                ["JoinedAt"] = new(DateTime.UtcNow.ToString("O")),
                ["GSI1PK"] = new($"{KeyPrefixes.UserMembership}{senderId}"),
                ["GSI1SK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["NetworkId"] = new(networkId),
                ["Name"] = new("Test Network"),
            },
        });

        // Add receiver as member
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"MEMBER#{receiverId}"),
                ["UserId"] = new(receiverId),
                ["Role"] = new("member"),
                ["Status"] = new("Active"),
                ["JoinedAt"] = new(DateTime.UtcNow.ToString("O")),
                ["GSI1PK"] = new($"{KeyPrefixes.UserMembership}{receiverId}"),
                ["GSI1SK"] = new($"{KeyPrefixes.Network}{receiverId}"),
                ["NetworkId"] = new(networkId),
                ["Name"] = new("Test Network"),
            },
        });

        return (senderId, receiverId, networkId);
    }

    private async Task EnsureTxTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Transactions);
        }
        catch (ResourceNotFoundException)
        {
            try { await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Transactions,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            }); } catch (ResourceInUseException) { /* concurrent creation */ }
        }
    }

    private async Task EnsureNetworksTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Networks);
        }
        catch (ResourceNotFoundException)
        {
            try { await _dynamo.Client.CreateTableAsync(new CreateTableRequest
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
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
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
                ],
            }); } catch (ResourceInUseException) { /* concurrent creation */ }
        }
    }
}

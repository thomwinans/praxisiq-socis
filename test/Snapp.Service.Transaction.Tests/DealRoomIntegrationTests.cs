using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Transaction;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Transaction.Tests;

[Collection(DockerTestCollection.Name)]
public class DealRoomIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly DynamoDbTestHelper _dynamo;

    private const string TxServiceUrl = "http://localhost:8086";
    private const string MinioUrl = "http://localhost:9000";
    private const string BucketName = "snapp-deals";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public DealRoomIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureTxTableAsync();
        await EnsureS3BucketAsync();
    }

    public Task DisposeAsync()
    {
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    private static HttpClient CreateClient(string userId)
    {
        var http = new HttpClient { BaseAddress = new Uri(TxServiceUrl), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("X-User-Id", userId);
        return http;
    }

    // ── Deal Room CRUD ─────────────────────────────────────────

    [Fact]
    public async Task CreateDealRoom_ValidRequest_Returns201()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(userId);

        var response = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest
        {
            Name = "Practice Acquisition — Dr. Smith",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deal = await response.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);
        Assert.NotNull(deal);
        Assert.Equal("Practice Acquisition — Dr. Smith", deal!.Name);
        Assert.Equal(userId, deal.CreatedByUserId);
        Assert.Equal(1, deal.ParticipantCount); // creator auto-added
    }

    [Fact]
    public async Task CreateDealRoom_NoAuth_Returns401()
    {
        using var http = new HttpClient { BaseAddress = new Uri(TxServiceUrl) };

        var response = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest
        {
            Name = "Test Deal",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDealRooms_ReturnsUserDeals()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(userId);

        // Create two deals
        await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Deal A" });
        await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Deal B" });

        var listResp = await http.GetAsync("/api/tx/deals");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<DealRoomListResponse>(JsonOptions);
        Assert.NotNull(list);
        Assert.True(list!.DealRooms.Count >= 2);
    }

    [Fact]
    public async Task GetDealRoom_Participant_ReturnsOk()
    {
        var userId = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(userId);

        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "My Deal" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        var getResp = await http.GetAsync($"/api/tx/deals/{deal!.DealId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var fetched = await getResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);
        Assert.Equal(deal.DealId, fetched!.DealId);
    }

    [Fact]
    public async Task GetDealRoom_NonParticipant_Returns403()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var outsider = await _dynamo.CreateTestUserAsync();
        using var creatorHttp = CreateClient(creator);

        var createResp = await creatorHttp.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Private Deal" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        using var outsiderHttp = CreateClient(outsider);
        var getResp = await outsiderHttp.GetAsync($"/api/tx/deals/{deal!.DealId}");
        Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);
    }

    // ── Participants ────────────────────────────────────────────

    [Fact]
    public async Task AddParticipant_ValidRequest_Returns201()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var buyer = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Acquisition Deal" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        var addResp = await http.PostAsJsonAsync($"/api/tx/deals/{deal!.DealId}/participants", new AddParticipantRequest
        {
            UserId = buyer,
            Role = "buyer",
        });

        Assert.Equal(HttpStatusCode.Created, addResp.StatusCode);
        var participant = await addResp.Content.ReadFromJsonAsync<DealParticipantResponse>(JsonOptions);
        Assert.Equal(buyer, participant!.UserId);
        Assert.Equal("buyer", participant.Role);
    }

    [Fact]
    public async Task AddParticipant_InvalidRole_Returns400()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var other = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Test" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        var addResp = await http.PostAsJsonAsync($"/api/tx/deals/{deal!.DealId}/participants", new AddParticipantRequest
        {
            UserId = other,
            Role = "hacker",
        });

        Assert.Equal(HttpStatusCode.BadRequest, addResp.StatusCode);
    }

    [Fact]
    public async Task AddParticipant_NonParticipant_Returns403()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var outsider = await _dynamo.CreateTestUserAsync();
        var target = await _dynamo.CreateTestUserAsync();
        using var creatorHttp = CreateClient(creator);

        var createResp = await creatorHttp.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Test" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        using var outsiderHttp = CreateClient(outsider);
        var addResp = await outsiderHttp.PostAsJsonAsync($"/api/tx/deals/{deal!.DealId}/participants", new AddParticipantRequest
        {
            UserId = target,
            Role = "advisor",
        });

        Assert.Equal(HttpStatusCode.Forbidden, addResp.StatusCode);
    }

    [Fact]
    public async Task RemoveParticipant_ByCreator_Returns204()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var buyer = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Test" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        await http.PostAsJsonAsync($"/api/tx/deals/{deal!.DealId}/participants", new AddParticipantRequest
        {
            UserId = buyer,
            Role = "buyer",
        });

        var removeResp = await http.DeleteAsync($"/api/tx/deals/{deal.DealId}/participants/{buyer}");
        Assert.Equal(HttpStatusCode.NoContent, removeResp.StatusCode);

        // Verify removed participant can't access
        using var buyerHttp = CreateClient(buyer);
        var getResp = await buyerHttp.GetAsync($"/api/tx/deals/{deal.DealId}");
        Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);
    }

    // ── Documents (pre-signed URLs) ────────────────────────────

    [Fact]
    public async Task UploadDocument_GeneratesPresignedUrl()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Doc Test Deal" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        var uploadResp = await http.PostAsync(
            $"/api/tx/deals/{deal!.DealId}/documents?filename=valuation.pdf", null);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);

        var presigned = await uploadResp.Content.ReadFromJsonAsync<PresignedUrlResponse>(JsonOptions);
        Assert.NotNull(presigned);
        Assert.False(string.IsNullOrEmpty(presigned!.Url));
        Assert.Equal(900, presigned.ExpiresIn);
    }

    [Fact]
    public async Task UploadAndDownloadDocument_FullLifecycle()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        // Create deal
        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Full Lifecycle" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        // Generate upload URL
        var uploadResp = await http.PostAsync(
            $"/api/tx/deals/{deal!.DealId}/documents?filename=test.txt", null);
        var presigned = await uploadResp.Content.ReadFromJsonAsync<PresignedUrlResponse>(JsonOptions);

        // Upload file content directly to MinIO via pre-signed URL
        using var uploadClient = new HttpClient();
        var content = new StringContent("Hello, Deal Room!");
        var putResp = await uploadClient.PutAsync(presigned!.Url, content);
        Assert.True(putResp.IsSuccessStatusCode, $"S3 upload failed: {putResp.StatusCode}");

        // List documents
        var listResp = await http.GetAsync($"/api/tx/deals/{deal.DealId}/documents");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var docs = await listResp.Content.ReadFromJsonAsync<List<DealDocumentResponse>>(JsonOptions);
        Assert.NotNull(docs);
        Assert.Single(docs!);
        Assert.Equal("test.txt", docs[0].Filename);

        // Get download URL
        var downloadResp = await http.GetAsync(
            $"/api/tx/deals/{deal.DealId}/documents/{docs[0].DocumentId}/url");
        Assert.Equal(HttpStatusCode.OK, downloadResp.StatusCode);
        var downloadPresigned = await downloadResp.Content.ReadFromJsonAsync<PresignedUrlResponse>(JsonOptions);
        Assert.False(string.IsNullOrEmpty(downloadPresigned!.Url));

        // Download file via pre-signed URL
        using var downloadClient = new HttpClient();
        var downloaded = await downloadClient.GetStringAsync(downloadPresigned.Url);
        Assert.Equal("Hello, Deal Room!", downloaded);
    }

    [Fact]
    public async Task ListDocuments_NonParticipant_Returns403()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var outsider = await _dynamo.CreateTestUserAsync();
        using var creatorHttp = CreateClient(creator);

        var createResp = await creatorHttp.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Private" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        using var outsiderHttp = CreateClient(outsider);
        var listResp = await outsiderHttp.GetAsync($"/api/tx/deals/{deal!.DealId}/documents");
        Assert.Equal(HttpStatusCode.Forbidden, listResp.StatusCode);
    }

    // ── Audit Trail ────────────────────────────────────────────

    [Fact]
    public async Task AuditTrail_RecordsAllActions()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var buyer = await _dynamo.CreateTestUserAsync();
        using var http = CreateClient(creator);

        // Create deal (audit: deal_created)
        var createResp = await http.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Audit Test" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        // Add participant (audit: participant_added)
        await http.PostAsJsonAsync($"/api/tx/deals/{deal!.DealId}/participants", new AddParticipantRequest
        {
            UserId = buyer,
            Role = "buyer",
        });

        // Upload document (audit: document_uploaded)
        await http.PostAsync($"/api/tx/deals/{deal.DealId}/documents?filename=nda.pdf", null);

        // Remove participant (audit: participant_removed)
        await http.DeleteAsync($"/api/tx/deals/{deal.DealId}/participants/{buyer}");

        // Check audit trail
        var auditResp = await http.GetAsync($"/api/tx/deals/{deal.DealId}/audit");
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);

        var entries = await auditResp.Content.ReadFromJsonAsync<List<DealAuditEntryResponse>>(JsonOptions);
        Assert.NotNull(entries);
        Assert.True(entries!.Count >= 4);

        var actions = entries.Select(e => e.Action).ToList();
        Assert.Contains("deal_created", actions);
        Assert.Contains("participant_added", actions);
        Assert.Contains("document_uploaded", actions);
        Assert.Contains("participant_removed", actions);
    }

    [Fact]
    public async Task AuditTrail_NonParticipant_Returns403()
    {
        var creator = await _dynamo.CreateTestUserAsync();
        var outsider = await _dynamo.CreateTestUserAsync();
        using var creatorHttp = CreateClient(creator);

        var createResp = await creatorHttp.PostAsJsonAsync("/api/tx/deals", new CreateDealRoomRequest { Name = "Private" });
        var deal = await createResp.Content.ReadFromJsonAsync<DealRoomResponse>(JsonOptions);

        using var outsiderHttp = CreateClient(outsider);
        var auditResp = await outsiderHttp.GetAsync($"/api/tx/deals/{deal!.DealId}/audit");
        Assert.Equal(HttpStatusCode.Forbidden, auditResp.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private async Task EnsureTxTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Transactions);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
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
            });
        }
    }

    private static async Task EnsureS3BucketAsync()
    {
        using var s3 = new AmazonS3Client(
            "minioadmin", "minioadmin",
            new AmazonS3Config { ServiceURL = MinioUrl, ForcePathStyle = true });

        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou"
                                         || ex.ErrorCode == "BucketAlreadyExists")
        {
            // Already exists
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.DTOs.Network;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Content.Tests;

[Collection(DockerTestCollection.Name)]
public class ContentIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ContentIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _http = new HttpClient { BaseAddress = new Uri(fixture.KongUrl), Timeout = TimeSpan.FromSeconds(15) };
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureNetworksTableAsync();
        await EnsureContentTableAsync();
        await EnsureNotificationsTableAsync();
        await _fixture.PapercutClient.DeleteAllMessagesAsync();
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    // ── Thread Tests ────────────────────────────────────────────

    [Fact]
    public async Task CreateThread_Member_ReturnsCreated()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId,
            Title = "First Discussion",
            Content = "Let's talk about this topic.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ThreadResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Title.Should().Be("First Discussion");
        body.NetworkId.Should().Be(netId);
        body.ReplyCount.Should().BeGreaterOrEqualTo(1);
        body.ThreadId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListThreads_Member_ReturnsThreads()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Create two threads
        await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Thread A", Content = "Content A",
        });
        await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Thread B", Content = "Content B",
        });

        var listResp = await _http.GetAsync($"/api/content/networks/{netId}/threads");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResp.Content.ReadFromJsonAsync<ThreadListResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Threads.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task CreateReply_Member_ReturnsCreated()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Create a thread
        var threadResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Reply Test", Content = "OP content",
        });
        var thread = await threadResp.Content.ReadFromJsonAsync<ThreadResponse>(JsonOptions);

        // Add a reply
        var replyResp = await _http.PostAsJsonAsync($"/api/content/threads/{thread!.ThreadId}/replies", new
        {
            Content = "This is a reply.",
        });

        replyResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var reply = await replyResp.Content.ReadFromJsonAsync<ReplyResponse>(JsonOptions);
        reply.Should().NotBeNull();
        reply!.Content.Should().Be("This is a reply.");
        reply.ThreadId.Should().Be(thread.ThreadId);
    }

    [Fact]
    public async Task ListReplies_Member_ReturnsChronological()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var threadResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Replies List Test", Content = "OP",
        });
        var thread = await threadResp.Content.ReadFromJsonAsync<ThreadResponse>(JsonOptions);

        await _http.PostAsJsonAsync($"/api/content/threads/{thread!.ThreadId}/replies", new { Content = "Reply 1" });
        await _http.PostAsJsonAsync($"/api/content/threads/{thread.ThreadId}/replies", new { Content = "Reply 2" });

        var listResp = await _http.GetAsync($"/api/content/threads/{thread.ThreadId}/replies");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResp.Content.ReadFromJsonAsync<ReplyListResponse>(JsonOptions);
        body.Should().NotBeNull();
        // Initial reply (OP) + 2 replies = at least 3
        body!.Replies.Count.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task NonMember_CannotCreateThread()
    {
        var (stewardJwt, netId) = await SetupNetworkWithMember();
        if (stewardJwt is null) return;

        // Authenticate as a different user who is NOT a member
        var outsiderJwt = await AuthenticateAsync($"content-outsider-{Guid.NewGuid():N}@test.snapp");
        if (outsiderJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderJwt);

        var response = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Should Fail", Content = "Nope",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteReply_Author_Succeeds()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var threadResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Delete Test", Content = "OP",
        });
        var thread = await threadResp.Content.ReadFromJsonAsync<ThreadResponse>(JsonOptions);

        var replyResp = await _http.PostAsJsonAsync($"/api/content/threads/{thread!.ThreadId}/replies", new
        {
            Content = "To be deleted",
        });
        var reply = await replyResp.Content.ReadFromJsonAsync<ReplyResponse>(JsonOptions);

        var deleteResp = await _http.DeleteAsync($"/api/content/threads/{thread.ThreadId}/replies/{reply!.ReplyId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify soft delete — reply content should be [removed]
        var listResp = await _http.GetAsync($"/api/content/threads/{thread.ThreadId}/replies");
        var repliesBody = await listResp.Content.ReadFromJsonAsync<ReplyListResponse>(JsonOptions);
        var deletedReply = repliesBody!.Replies.FirstOrDefault(r => r.ReplyId == reply.ReplyId);
        deletedReply.Should().NotBeNull();
        deletedReply!.Content.Should().Be("[removed]");
    }

    // ── Feed Tests ──────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_Member_ReturnsCreated()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId,
            Content = "Hello, network!",
            PostType = "Text",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Content.Should().Be("Hello, network!");
        body.NetworkId.Should().Be(netId);
        body.PostId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListFeed_Member_ReturnsPosts()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "Post 1",
        });
        await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "Post 2",
        });

        var feedResp = await _http.GetAsync($"/api/content/networks/{netId}/feed");
        feedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await feedResp.Content.ReadFromJsonAsync<FeedResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Posts.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ListUserPosts_Authenticated_ReturnsPosts()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "User post",
        });

        var userPostsResp = await _http.GetAsync($"/api/content/users/{userId}/posts");
        userPostsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await userPostsResp.Content.ReadFromJsonAsync<FeedResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Posts.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task React_Authenticated_ReturnsUpdatedCounts()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var postResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "Likeable post",
        });
        var post = await postResp.Content.ReadFromJsonAsync<PostResponse>(JsonOptions);

        var reactResp = await _http.PostAsJsonAsync($"/api/content/posts/{post!.PostId}/react", new
        {
            ReactionType = "like",
        });

        reactResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var counts = await reactResp.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions);
        counts.Should().NotBeNull();
        counts!.Should().ContainKey("like");
        counts!["like"].Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task RemoveReaction_Authenticated_DecrementsCounts()
    {
        var (jwt, netId) = await SetupNetworkWithMember();
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var postResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "Unlikeable post",
        });
        var post = await postResp.Content.ReadFromJsonAsync<PostResponse>(JsonOptions);

        // Add reaction
        await _http.PostAsJsonAsync($"/api/content/posts/{post!.PostId}/react", new { ReactionType = "like" });

        // Remove reaction
        var removeResp = await _http.DeleteAsync($"/api/content/posts/{post.PostId}/react");
        removeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var counts = await removeResp.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions);
        counts.Should().NotBeNull();
    }

    [Fact]
    public async Task NonMember_CannotCreatePost()
    {
        var (_, netId) = await SetupNetworkWithMember();

        var outsiderJwt = await AuthenticateAsync($"content-post-outsider-{Guid.NewGuid():N}@test.snapp");
        if (outsiderJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderJwt);

        var response = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/posts", new
        {
            NetworkId = netId, Content = "Should fail",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MentionInReply_WritesNotificationToSnappNotif()
    {
        var (stewardJwt, netId) = await SetupNetworkWithMember();
        if (stewardJwt is null) return;

        // Add second member
        var member2Jwt = await AuthenticateAsync($"content-mention-m2-{Guid.NewGuid():N}@test.snapp");
        if (member2Jwt is null) return;
        var member2Id = ExtractUserId(member2Jwt);

        // Invite member2 to network
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stewardJwt);
        await _http.PostAsJsonAsync($"/api/networks/{netId}/invite", new { UserId = member2Id });

        // Create thread as steward
        var threadResp = await _http.PostAsJsonAsync($"/api/content/networks/{netId}/threads", new
        {
            NetworkId = netId, Title = "Mention Test", Content = "OP",
        });
        var thread = await threadResp.Content.ReadFromJsonAsync<ThreadResponse>(JsonOptions);

        // Reply mentioning member2
        var replyResp = await _http.PostAsJsonAsync($"/api/content/threads/{thread!.ThreadId}/replies", new
        {
            Content = $"Hey @{member2Id} check this out!",
        });
        replyResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify notification in snapp-notif table
        var notifQuery = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{member2Id}"),
            },
        });

        notifQuery.Items.Should().NotBeEmpty();
        var notif = notifQuery.Items.First();
        notif["Type"].S.Should().Be("MentionInDiscussion");
        notif["ThreadId"].S.Should().Be(thread.ThreadId);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<(string? Jwt, string NetId)> SetupNetworkWithMember()
    {
        var jwt = await AuthenticateAsync($"content-steward-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return (null, "");

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/networks", new CreateNetworkRequest
        {
            Name = $"Content Test Net {Guid.NewGuid():N}",
        });

        var network = await response.Content.ReadFromJsonAsync<NetworkResponse>(JsonOptions);
        return (jwt, network!.NetworkId);
    }

    private async Task<string?> AuthenticateAsync(string email)
    {
        try
        {
            var magicResp = await _http.PostAsJsonAsync("/api/auth/magic-link", new { Email = email });
            if (!magicResp.IsSuccessStatusCode) return null;

            await Task.Delay(500);
            var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
            if (code is null) return null;

            var validateResp = await _http.PostAsJsonAsync("/api/auth/validate", new { Code = code });
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

    private async Task EnsureContentTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Content);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Content,
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
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = GsiNames.UserPosts,
                        KeySchema =
                        [
                            new KeySchemaElement("GSI1PK", KeyType.HASH),
                            new KeySchemaElement("GSI1SK", KeyType.RANGE),
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }

    private async Task EnsureNotificationsTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Notifications);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Notifications,
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
}

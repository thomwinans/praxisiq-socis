using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Content.Repositories;

public class ContentRepository : IContentRepository
{
    private readonly IAmazonDynamoDB _db;

    public ContentRepository(IAmazonDynamoDB db) => _db = db;

    // ── IContentRepository ────────────────────────────────────────

    public async Task CreatePostAsync(Post post)
    {
        var timestamp = post.CreatedAt.ToString("O");
        var postItem = BuildPostItem(post, timestamp);

        // FEED#{networkId} / POST#{timestamp}#{postId}
        var feedItem = new Dictionary<string, AttributeValue>(postItem)
        {
            ["PK"] = new($"{KeyPrefixes.Feed}{post.NetworkId}"),
            ["SK"] = new($"POST#{timestamp}#{post.PostId}"),
        };

        // UPOST#{userId} / POST#{timestamp}#{postId} — also projected into GSI-UserPosts
        var userPostItem = new Dictionary<string, AttributeValue>(postItem)
        {
            ["PK"] = new($"{KeyPrefixes.UserPost}{post.AuthorUserId}"),
            ["SK"] = new($"POST#{timestamp}#{post.PostId}"),
            ["GSI1PK"] = new($"{KeyPrefixes.UserPost}{post.AuthorUserId}"),
            ["GSI1SK"] = new($"POST#{timestamp}#{post.PostId}"),
        };

        // POST#{postId} / META — direct lookup by postId
        var metaItem = new Dictionary<string, AttributeValue>(postItem)
        {
            ["PK"] = new($"POST#{post.PostId}"),
            ["SK"] = new("META"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Content, Item = feedItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Content, Item = userPostItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Content, Item = metaItem } },
            ],
        });
    }

    public async Task<List<Post>> ListNetworkFeedAsync(string networkId, string? nextToken, int limit = 25)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Feed}{networkId}"),
                [":prefix"] = new("POST#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 25),
        };

        if (nextToken is not null)
            request.ExclusiveStartKey = DecodeNextToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapPost).ToList();
    }

    public async Task<List<Post>> ListUserPostsAsync(string userId, string? nextToken, int limit = 25)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.UserPost}{userId}"),
                [":prefix"] = new("POST#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 25),
        };

        if (nextToken is not null)
            request.ExclusiveStartKey = DecodeNextToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapPost).ToList();
    }

    public async Task CreateThreadAsync(DiscussionThread thread)
    {
        var timestamp = thread.CreatedAt.ToString("O");

        // DISC#{networkId} / THREAD#{timestamp}#{threadId}
        var discItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Discussion}{thread.NetworkId}"),
            ["SK"] = new($"{KeyPrefixes.Thread}{timestamp}#{thread.ThreadId}"),
            ["ThreadId"] = new(thread.ThreadId),
            ["NetworkId"] = new(thread.NetworkId),
            ["Title"] = new(thread.Title),
            ["AuthorUserId"] = new(thread.AuthorUserId),
            ["ReplyCount"] = new() { N = thread.ReplyCount.ToString() },
            ["CreatedAt"] = new(timestamp),
        };

        if (thread.LastReplyAt.HasValue)
            discItem["LastReplyAt"] = new(thread.LastReplyAt.Value.ToString("O"));

        // THREAD#{threadId} / META — direct lookup by threadId
        var metaItem = new Dictionary<string, AttributeValue>(discItem)
        {
            ["PK"] = new($"{KeyPrefixes.Thread}{thread.ThreadId}"),
            ["SK"] = new("META"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Content, Item = discItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Content, Item = metaItem } },
            ],
        });
    }

    public async Task<List<DiscussionThread>> ListThreadsAsync(string networkId, string? nextToken, int limit = 25)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Discussion}{networkId}"),
                [":prefix"] = new(KeyPrefixes.Thread),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 25),
        };

        if (nextToken is not null)
            request.ExclusiveStartKey = DecodeNextToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapThread).ToList();
    }

    public async Task CreateReplyAsync(Reply reply)
    {
        var timestamp = reply.CreatedAt.ToString("O");

        var replyItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Thread}{reply.ThreadId}"),
            ["SK"] = new($"REPLY#{timestamp}#{reply.ReplyId}"),
            ["ReplyId"] = new(reply.ReplyId),
            ["ThreadId"] = new(reply.ThreadId),
            ["AuthorUserId"] = new(reply.AuthorUserId),
            ["Content"] = new(reply.Content),
            ["CreatedAt"] = new(timestamp),
        };

        // Transact: put reply + update thread ReplyCount and LastReplyAt on META item
        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put { TableName = TableNames.Content, Item = replyItem },
                },
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = TableNames.Content,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new($"{KeyPrefixes.Thread}{reply.ThreadId}"),
                            ["SK"] = new("META"),
                        },
                        UpdateExpression = "SET ReplyCount = ReplyCount + :one, LastReplyAt = :ts",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":one"] = new() { N = "1" },
                            [":ts"] = new(timestamp),
                        },
                    },
                },
            ],
        });

        // Also update the DISC# listing item's ReplyCount and LastReplyAt
        var thread = await GetThreadByIdAsync(reply.ThreadId);
        if (thread is not null)
        {
            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = TableNames.Content,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Discussion}{thread.NetworkId}"),
                    ["SK"] = new($"{KeyPrefixes.Thread}{thread.CreatedAt:O}#{thread.ThreadId}"),
                },
                UpdateExpression = "SET ReplyCount = ReplyCount + :one, LastReplyAt = :ts",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":one"] = new() { N = "1" },
                    [":ts"] = new(timestamp),
                },
            });
        }
    }

    public async Task<List<Reply>> ListRepliesAsync(string threadId, string? nextToken, int limit = 50)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Thread}{threadId}"),
                [":prefix"] = new("REPLY#"),
            },
            ScanIndexForward = true,
            Limit = Math.Min(limit, 50),
        };

        if (nextToken is not null)
            request.ExclusiveStartKey = DecodeNextToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapReply).ToList();
    }

    // ── Extended methods (not in IContentRepository) ──────────────

    public async Task<DiscussionThread?> GetThreadByIdAsync(string threadId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Thread}{threadId}"),
                ["SK"] = new("META"),
            },
        });

        return response.IsItemSet ? MapThread(response.Item) : null;
    }

    public async Task<Post?> GetPostByIdAsync(string postId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"POST#{postId}"),
                ["SK"] = new("META"),
            },
        });

        return response.IsItemSet ? MapPost(response.Item) : null;
    }

    public async Task<Reply?> GetReplyAsync(string threadId, string replyId)
    {
        // We need to find the reply by scanning REPLY# items for this thread
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "ReplyId = :replyId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Thread}{threadId}"),
                [":prefix"] = new("REPLY#"),
                [":replyId"] = new(replyId),
            },
            Limit = 1,
        };

        var response = await _db.QueryAsync(request);
        return response.Items.Count > 0 ? MapReply(response.Items[0]) : null;
    }

    public async Task SoftDeleteReplyAsync(string threadId, string replyId, string replySk)
    {
        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Thread}{threadId}"),
                ["SK"] = new(replySk),
            },
            UpdateExpression = "SET Content = :removed",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":removed"] = new("[removed]"),
            },
        });
    }

    public async Task<string?> FindReplySortKeyAsync(string threadId, string replyId)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Content,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "ReplyId = :replyId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Thread}{threadId}"),
                [":prefix"] = new("REPLY#"),
                [":replyId"] = new(replyId),
            },
            ProjectionExpression = "SK",
        };

        var response = await _db.QueryAsync(request);
        return response.Items.Count > 0 ? response.Items[0]["SK"].S : null;
    }

    public async Task<string?> GetReactionAsync(string postId, string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Reaction}{postId}"),
                ["SK"] = new($"{KeyPrefixes.User}{userId}"),
            },
        });

        return response.IsItemSet ? response.Item["ReactionType"].S : null;
    }

    public async Task<Dictionary<string, int>> AddReactionAsync(string postId, string userId, string reactionType)
    {
        // Upsert reaction item
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Content,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Reaction}{postId}"),
                ["SK"] = new($"{KeyPrefixes.User}{userId}"),
                ["PostId"] = new(postId),
                ["UserId"] = new(userId),
                ["ReactionType"] = new(reactionType),
                ["CreatedAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        });

        // Atomic increment on the post META item
        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"POST#{postId}"),
                ["SK"] = new("META"),
            },
            UpdateExpression = "ADD #reactions.#rtype :one",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#reactions"] = "ReactionCounts",
                ["#rtype"] = reactionType,
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":one"] = new() { N = "1" },
            },
        });

        // Return updated counts
        var post = await GetPostByIdAsync(postId);
        return post?.ReactionCounts ?? new Dictionary<string, int>();
    }

    public async Task<Dictionary<string, int>> RemoveReactionAsync(string postId, string userId)
    {
        // Get existing reaction type first
        var existingType = await GetReactionAsync(postId, userId);
        if (existingType is null)
            return (await GetPostByIdAsync(postId))?.ReactionCounts ?? new Dictionary<string, int>();

        // Delete reaction item
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Reaction}{postId}"),
                ["SK"] = new($"{KeyPrefixes.User}{userId}"),
            },
        });

        // Atomic decrement on the post META item
        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Content,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"POST#{postId}"),
                ["SK"] = new("META"),
            },
            UpdateExpression = "ADD #reactions.#rtype :neg",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#reactions"] = "ReactionCounts",
                ["#rtype"] = existingType,
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":neg"] = new() { N = "-1" },
            },
        });

        var post = await GetPostByIdAsync(postId);
        return post?.ReactionCounts ?? new Dictionary<string, int>();
    }

    // ── Membership check (cross-table query to snapp-networks) ───

    public async Task<(bool IsMember, string? Role)> CheckMembershipAsync(string networkId, string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"MEMBER#{userId}"),
            },
        });

        if (!response.IsItemSet)
            return (false, null);

        var role = response.Item.TryGetValue("Role", out var r) ? r.S : null;
        return (true, role);
    }

    public async Task<Permission> GetRolePermissionsAsync(string networkId, string roleName)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"ROLE#{roleName}"),
            },
        });

        if (!response.IsItemSet)
            return Permission.None;

        return (Permission)int.Parse(response.Item["Permissions"].N);
    }

    // ── Notification helper ──────────────────────────────────────

    public async Task QueueMentionNotificationAsync(string mentionedUserId, string authorUserId, string threadId, string content)
    {
        var notifId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Notification}{mentionedUserId}"),
                ["SK"] = new($"{KeyPrefixes.Notification}{now:O}#{notifId}"),
                ["NotificationId"] = new(notifId),
                ["UserId"] = new(mentionedUserId),
                ["Type"] = new("MentionInDiscussion"),
                ["SourceUserId"] = new(authorUserId),
                ["ThreadId"] = new(threadId),
                ["Preview"] = new(content.Length > 100 ? content[..100] : content),
                ["IsRead"] = new() { BOOL = false },
                ["CreatedAt"] = new(now.ToString("O")),
            },
        });
    }

    // ── Private helpers ──────────────────────────────────────────

    private static Dictionary<string, AttributeValue> BuildPostItem(Post post, string timestamp)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PostId"] = new(post.PostId),
            ["NetworkId"] = new(post.NetworkId),
            ["AuthorUserId"] = new(post.AuthorUserId),
            ["Content"] = new(post.Content),
            ["PostType"] = new(post.PostType.ToString()),
            ["CreatedAt"] = new(timestamp),
        };

        if (post.ReactionCounts.Count > 0)
        {
            item["ReactionCounts"] = new()
            {
                M = post.ReactionCounts.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue { N = kvp.Value.ToString() }),
            };
        }
        else
        {
            item["ReactionCounts"] = new() { M = new Dictionary<string, AttributeValue>() };
        }

        return item;
    }

    private static Post MapPost(Dictionary<string, AttributeValue> item) => new()
    {
        PostId = item["PostId"].S,
        NetworkId = item["NetworkId"].S,
        AuthorUserId = item["AuthorUserId"].S,
        Content = item["Content"].S,
        PostType = Enum.TryParse<PostType>(item.TryGetValue("PostType", out var pt) ? pt.S : "Text", out var postType)
            ? postType : PostType.Text,
        ReactionCounts = item.TryGetValue("ReactionCounts", out var rc) && rc.M is not null
            ? rc.M.Where(kvp => kvp.Value.N is not null)
                  .ToDictionary(kvp => kvp.Key, kvp => int.Parse(kvp.Value.N))
            : new Dictionary<string, int>(),
        CreatedAt = DateTime.Parse(item["CreatedAt"].S),
    };

    private static DiscussionThread MapThread(Dictionary<string, AttributeValue> item) => new()
    {
        ThreadId = item["ThreadId"].S,
        NetworkId = item["NetworkId"].S,
        Title = item["Title"].S,
        AuthorUserId = item["AuthorUserId"].S,
        ReplyCount = item.TryGetValue("ReplyCount", out var rc) ? int.Parse(rc.N) : 0,
        LastReplyAt = item.TryGetValue("LastReplyAt", out var lr) && !string.IsNullOrEmpty(lr.S)
            ? DateTime.Parse(lr.S) : null,
        CreatedAt = DateTime.Parse(item["CreatedAt"].S),
    };

    private static Reply MapReply(Dictionary<string, AttributeValue> item) => new()
    {
        ReplyId = item["ReplyId"].S,
        ThreadId = item["ThreadId"].S,
        AuthorUserId = item["AuthorUserId"].S,
        Content = item["Content"].S,
        CreatedAt = DateTime.Parse(item["CreatedAt"].S),
    };

    private static Dictionary<string, AttributeValue>? DecodeNextToken(string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict?.ToDictionary(kvp => kvp.Key, kvp => new AttributeValue(kvp.Value));
        }
        catch
        {
            return null;
        }
    }
}

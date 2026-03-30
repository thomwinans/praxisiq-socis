using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-content DynamoDB table.
/// Handles posts, discussion threads, and replies.
/// </summary>
public interface IContentRepository
{
    /// <summary>Creates a new post in both the network feed and user post index.</summary>
    Task CreatePostAsync(Post post);

    /// <summary>Lists posts in a network feed, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<Post>> ListNetworkFeedAsync(string networkId, string? nextToken, int limit = 25);

    /// <summary>Lists a user's posts across all networks via GSI-UserPosts. Supports pagination.</summary>
    Task<List<Post>> ListUserPostsAsync(string userId, string? nextToken, int limit = 25);

    /// <summary>Creates a new discussion thread in a network.</summary>
    Task CreateThreadAsync(DiscussionThread thread);

    /// <summary>Lists discussion threads in a network, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<DiscussionThread>> ListThreadsAsync(string networkId, string? nextToken, int limit = 25);

    /// <summary>Creates a new reply on a discussion thread and increments the thread's reply count.</summary>
    Task CreateReplyAsync(Reply reply);

    /// <summary>Lists replies on a thread, ordered by timestamp ascending. Supports pagination.</summary>
    Task<List<Reply>> ListRepliesAsync(string threadId, string? nextToken, int limit = 50);
}

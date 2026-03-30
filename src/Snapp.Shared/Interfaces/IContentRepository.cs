using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface IContentRepository
{
    Task CreatePostAsync(Post post);

    Task<List<Post>> ListNetworkFeedAsync(string networkId, string? nextToken, int limit = 25);

    Task<List<Post>> ListUserPostsAsync(string userId, string? nextToken, int limit = 25);

    Task CreateThreadAsync(DiscussionThread thread);

    Task<List<DiscussionThread>> ListThreadsAsync(string networkId, string? nextToken, int limit = 25);

    Task CreateReplyAsync(Reply reply);

    Task<List<Reply>> ListRepliesAsync(string threadId, string? nextToken, int limit = 50);
}

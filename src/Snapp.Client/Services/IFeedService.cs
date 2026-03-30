using Snapp.Shared.DTOs.Content;

namespace Snapp.Client.Services;

public interface IFeedService
{
    Task<FeedResponse> GetFeedAsync(string networkId, string? nextToken = null, int limit = 25);
    Task<PostResponse?> CreatePostAsync(string networkId, CreatePostRequest request);
    Task<bool> ReactAsync(string postId, string reactionType);
    Task<bool> RemoveReactionAsync(string postId, string reactionType);
}

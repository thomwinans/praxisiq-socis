using Snapp.Shared.DTOs.Content;

namespace Snapp.Client.Services;

public interface IDiscussionService
{
    Task<ThreadListResponse> GetThreadsAsync(string networkId, string? nextToken = null, int limit = 25);
    Task<ThreadResponse?> GetThreadAsync(string threadId);
    Task<ThreadResponse?> CreateThreadAsync(string networkId, CreateThreadRequest request);
    Task<ReplyListResponse> GetRepliesAsync(string threadId, string? nextToken = null, int limit = 50);
    Task<ReplyResponse?> CreateReplyAsync(string threadId, CreateReplyRequest request);
    Task<bool> DeleteReplyAsync(string threadId, string replyId);
}

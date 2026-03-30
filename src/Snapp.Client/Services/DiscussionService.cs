using System.Net.Http.Json;
using Snapp.Shared.DTOs.Content;

namespace Snapp.Client.Services;

public class DiscussionService : IDiscussionService
{
    private readonly HttpClient _http;

    public DiscussionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ThreadListResponse> GetThreadsAsync(string networkId, string? nextToken = null, int limit = 25)
    {
        var url = $"content/networks/{networkId}/threads?limit={limit}";
        if (!string.IsNullOrEmpty(nextToken))
            url += $"&nextToken={Uri.EscapeDataString(nextToken)}";

        return await _http.GetFromJsonAsync<ThreadListResponse>(url)
               ?? new ThreadListResponse();
    }

    public async Task<ThreadResponse?> GetThreadAsync(string threadId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ThreadResponse>($"content/threads/{threadId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ThreadResponse?> CreateThreadAsync(string networkId, CreateThreadRequest request)
    {
        var response = await _http.PostAsJsonAsync($"content/networks/{networkId}/threads", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ThreadResponse>();
        return null;
    }

    public async Task<ReplyListResponse> GetRepliesAsync(string threadId, string? nextToken = null, int limit = 50)
    {
        var url = $"content/threads/{threadId}/replies?limit={limit}";
        if (!string.IsNullOrEmpty(nextToken))
            url += $"&nextToken={Uri.EscapeDataString(nextToken)}";

        return await _http.GetFromJsonAsync<ReplyListResponse>(url)
               ?? new ReplyListResponse();
    }

    public async Task<ReplyResponse?> CreateReplyAsync(string threadId, CreateReplyRequest request)
    {
        var response = await _http.PostAsJsonAsync($"content/threads/{threadId}/replies", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ReplyResponse>();
        return null;
    }

    public async Task<bool> DeleteReplyAsync(string threadId, string replyId)
    {
        var response = await _http.DeleteAsync($"content/threads/{threadId}/replies/{replyId}");
        return response.IsSuccessStatusCode;
    }
}

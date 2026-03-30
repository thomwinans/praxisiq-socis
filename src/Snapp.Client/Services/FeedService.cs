using System.Net.Http.Json;
using Snapp.Shared.DTOs.Content;

namespace Snapp.Client.Services;

public class FeedService : IFeedService
{
    private readonly HttpClient _http;

    public FeedService(HttpClient http)
    {
        _http = http;
    }

    public async Task<FeedResponse> GetFeedAsync(string networkId, string? nextToken = null, int limit = 25)
    {
        var url = $"content/networks/{networkId}/feed?limit={limit}";
        if (!string.IsNullOrEmpty(nextToken))
            url += $"&nextToken={Uri.EscapeDataString(nextToken)}";

        return await _http.GetFromJsonAsync<FeedResponse>(url)
               ?? new FeedResponse();
    }

    public async Task<PostResponse?> CreatePostAsync(string networkId, CreatePostRequest request)
    {
        var response = await _http.PostAsJsonAsync($"content/networks/{networkId}/posts", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PostResponse>();
        return null;
    }

    public async Task<bool> ReactAsync(string postId, string reactionType)
    {
        var response = await _http.PostAsync($"content/posts/{postId}/reactions/{reactionType}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveReactionAsync(string postId, string reactionType)
    {
        var response = await _http.DeleteAsync($"content/posts/{postId}/reactions/{reactionType}");
        return response.IsSuccessStatusCode;
    }
}

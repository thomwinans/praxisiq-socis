namespace Snapp.Shared.DTOs.Content;

public class FeedResponse
{
    public List<PostResponse> Posts { get; set; } = new();

    public string? NextToken { get; set; }
}

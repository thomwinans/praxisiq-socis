namespace Snapp.Shared.DTOs.Content;

public class ReplyListResponse
{
    public List<ReplyResponse> Replies { get; set; } = new();

    public string? NextToken { get; set; }
}

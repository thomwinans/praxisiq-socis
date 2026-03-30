namespace Snapp.Shared.DTOs.Content;

public class ReplyResponse
{
    public string ReplyId { get; set; } = string.Empty;

    public string ThreadId { get; set; } = string.Empty;

    public string AuthorUserId { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

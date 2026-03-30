namespace Snapp.Shared.DTOs.Content;

public class ThreadResponse
{
    public string ThreadId { get; set; } = string.Empty;

    public string NetworkId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AuthorUserId { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public int ReplyCount { get; set; }

    public DateTime? LastReplyAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

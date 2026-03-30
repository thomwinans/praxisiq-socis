using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Content;

public class PostResponse
{
    public string PostId { get; set; } = string.Empty;

    public string NetworkId { get; set; } = string.Empty;

    public string AuthorUserId { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public PostType PostType { get; set; }

    public Dictionary<string, int> ReactionCounts { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

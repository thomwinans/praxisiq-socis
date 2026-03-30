using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class DiscussionThread
{
    [Required]
    public string ThreadId { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    public int ReplyCount { get; set; }

    public DateTime? LastReplyAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

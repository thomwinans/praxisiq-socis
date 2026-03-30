using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class Reply
{
    [Required]
    public string ReplyId { get; set; } = string.Empty;

    [Required]
    public string ThreadId { get; set; } = string.Empty;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

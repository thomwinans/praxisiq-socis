using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class Post
{
    [Required]
    public string PostId { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    public PostType PostType { get; set; } = PostType.Text;

    public Dictionary<string, int> ReactionCounts { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

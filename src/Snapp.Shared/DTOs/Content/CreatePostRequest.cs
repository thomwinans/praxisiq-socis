using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Content;

public class CreatePostRequest
{
    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    public PostType PostType { get; set; } = PostType.Text;
}

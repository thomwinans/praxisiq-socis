using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Content;

public class CreateReplyRequest
{
    [Required]
    public string ThreadId { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
}

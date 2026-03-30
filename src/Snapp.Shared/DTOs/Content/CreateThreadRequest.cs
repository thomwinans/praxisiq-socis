using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Content;

public class CreateThreadRequest
{
    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(10000)]
    public string Content { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.LinkedIn;

public class LinkedInShareRequest
{
    [Required, MaxLength(3000)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    /// <summary>Type of content being shared: "post" or "milestone".</summary>
    [Required]
    public string SourceType { get; set; } = string.Empty;
}

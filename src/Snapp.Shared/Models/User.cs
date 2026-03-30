using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class User
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? Geography { get; set; }

    [Range(0, 100)]
    public decimal ProfileCompleteness { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

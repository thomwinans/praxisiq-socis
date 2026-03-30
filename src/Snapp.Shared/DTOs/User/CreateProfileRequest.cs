using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class CreateProfileRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Specialty { get; set; }

    [MaxLength(200)]
    public string? Geography { get; set; }
}

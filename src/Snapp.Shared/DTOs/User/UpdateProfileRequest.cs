using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class UpdateProfileRequest
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(100)]
    public string? Specialty { get; set; }

    [MaxLength(200)]
    public string? Geography { get; set; }

    [Url, MaxLength(500)]
    public string? PhotoUrl { get; set; }
}

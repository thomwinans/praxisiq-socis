using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Intelligence;

public class BenchmarkRequest
{
    [Required, MaxLength(100)]
    public string Specialty { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Geography { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string SizeBand { get; set; } = string.Empty;
}

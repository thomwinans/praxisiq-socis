using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Intelligence;

public class BenchmarkRequest
{
    [Required]
    public string Specialty { get; set; } = string.Empty;

    [Required]
    public string Geography { get; set; } = string.Empty;

    [Required]
    public string SizeBand { get; set; } = string.Empty;
}

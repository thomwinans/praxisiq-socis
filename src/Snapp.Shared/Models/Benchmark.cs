using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class Benchmark
{
    public string? Vertical { get; set; }

    [Required]
    public string Geography { get; set; } = string.Empty;

    public string? GeographicLevel { get; set; }

    public string? Specialty { get; set; }

    public string? SizeBand { get; set; }

    [Required]
    public string MetricName { get; set; } = string.Empty;

    public decimal P25 { get; set; }

    public decimal P50 { get; set; }

    public decimal P75 { get; set; }

    public decimal? Mean { get; set; }

    public int SampleSize { get; set; }

    public DateTime ComputedAt { get; set; }
}

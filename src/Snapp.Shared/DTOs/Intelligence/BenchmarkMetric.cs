namespace Snapp.Shared.DTOs.Intelligence;

public class BenchmarkMetric
{
    public string Name { get; set; } = string.Empty;

    public decimal P25 { get; set; }

    public decimal P50 { get; set; }

    public decimal P75 { get; set; }

    public decimal? UserValue { get; set; }

    public decimal? UserPercentile { get; set; }
}

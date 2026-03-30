namespace Snapp.Shared.DTOs.Intelligence;

public class BenchmarkResponse
{
    public List<BenchmarkMetric> Metrics { get; set; } = new();

    public int CohortSize { get; set; }
}

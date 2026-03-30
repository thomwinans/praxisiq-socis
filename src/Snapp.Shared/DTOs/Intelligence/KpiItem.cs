namespace Snapp.Shared.DTOs.Intelligence;

public class KpiItem
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Unit { get; set; }

    public string Trend { get; set; } = "Flat";

    public decimal? Percentile { get; set; }
}

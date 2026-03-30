namespace Snapp.Shared.DTOs.Intelligence;

public class DashboardResponse
{
    public List<KpiItem> KPIs { get; set; } = new();

    public decimal ConfidenceScore { get; set; }

    public ValuationSummary? ValuationSummary { get; set; }
}

public class ValuationSummary
{
    public decimal Downside { get; set; }

    public decimal Base { get; set; }

    public decimal Upside { get; set; }

    public decimal ConfidenceScore { get; set; }
}

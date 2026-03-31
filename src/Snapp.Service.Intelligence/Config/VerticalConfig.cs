namespace Snapp.Service.Intelligence.Config;

public class VerticalConfig
{
    public string Vertical { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<DimensionConfig> Dimensions { get; set; } = new();
    public List<ContributionCategoryConfig> ContributionCategories { get; set; } = new();
    public decimal BaseConfidence { get; set; } = 40m;
    public decimal MaxConfidence { get; set; } = 95m;
}

public class DimensionConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public Dictionary<string, decimal> Thresholds { get; set; } = new();
    public List<KpiConfig> Kpis { get; set; } = new();
}

public class KpiConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ContributionCategoryConfig
{
    public string Category { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public decimal ConfidenceWeight { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

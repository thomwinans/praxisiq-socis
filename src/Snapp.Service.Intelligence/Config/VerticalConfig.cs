namespace Snapp.Service.Intelligence.Config;

public class VerticalConfig
{
    public string Vertical { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<DimensionConfig> Dimensions { get; set; } = new();
    public List<ContributionCategoryConfig> ContributionCategories { get; set; } = new();
    public decimal BaseConfidence { get; set; } = 40m;
    public decimal MaxConfidence { get; set; } = 95m;
    public List<CareerStageRule> CareerStageRules { get; set; } = new();
    public List<CompensationRoleConfig> CompensationRoles { get; set; } = new();
}

public class CompensationRoleConfig
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> CompensationTypes { get; set; } = new();
    public List<string> AmountBands { get; set; } = new();
}

public class CareerStageRule
{
    public string Stage { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public decimal? MinTenureYears { get; set; }
    public decimal? MaxTenureYears { get; set; }
    public int? MinProviderCount { get; set; }
    public int? MaxProviderCount { get; set; }
    public int? MinLocationCount { get; set; }
    public decimal? MinProductionVolume { get; set; }
    public decimal? MaxOwnerProductionPct { get; set; }
    public decimal? MinOwnerProductionPct { get; set; }
    public bool? RequiresCoLocation { get; set; }
    public bool? RequiresEntityFormation { get; set; }
    public bool? RequiresSuccessionPlan { get; set; }
    public decimal? MaxCeHoursRecent { get; set; }
    public List<string> EntityTypes { get; set; } = new();
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

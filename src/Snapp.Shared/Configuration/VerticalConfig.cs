using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Snapp.Shared.Configuration;

/// <summary>
/// Root configuration for a vertical (industry) pack.
/// Loaded from JSON files in Config/verticals/{vertical}.json.
/// </summary>
public class VerticalConfig
{
    [Required, MaxLength(50)]
    public string Vertical { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<DimensionConfig> Dimensions { get; set; } = new();

    [Required, MinLength(1)]
    public List<ContributionCategoryConfig> ContributionCategories { get; set; } = new();

    [Required, MinLength(1)]
    public List<CareerStageRule> CareerStageRules { get; set; } = new();

    [Required, MinLength(1)]
    public List<BenchmarkMetricConfig> BenchmarkMetrics { get; set; } = new();

    [Required, MinLength(1)]
    public List<ValuationDriverConfig> ValuationDrivers { get; set; } = new();

    [Required]
    public RegistrySourceConfig RegistrySource { get; set; } = new();

    [Required, MinLength(1)]
    public List<RoleDefinitionConfig> RoleDefinitions { get; set; } = new();

    [Range(0, 100)]
    public decimal BaseConfidence { get; set; } = 40m;

    [Range(0, 100)]
    public decimal MaxConfidence { get; set; } = 95m;
}

public class DimensionConfig
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Range(0, 1)]
    public decimal Weight { get; set; }

    [Required]
    public ThresholdConfig Thresholds { get; set; } = new();

    [Required, MinLength(1)]
    public List<KpiConfig> Kpis { get; set; } = new();
}

public class ThresholdConfig
{
    [Range(0, 100)]
    public decimal Strong { get; set; }

    [Range(0, 100)]
    public decimal Acceptable { get; set; }

    [Range(0, 100)]
    public decimal Weak { get; set; }
}

public class KpiConfig
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;
}

public class ContributionCategoryConfig
{
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Dimension { get; set; } = string.Empty;

    [Range(0, 1)]
    public decimal ConfidenceWeight { get; set; }

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<ContributionFieldConfig> Fields { get; set; } = new();
}

public class ContributionFieldConfig
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    public FieldValidationConfig? Validation { get; set; }

    [Range(0, 1)]
    public decimal ConfidenceWeight { get; set; }
}

public class FieldValidationConfig
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }

    [MaxLength(200)]
    public string? Pattern { get; set; }

    public bool Required { get; set; }
}

public class CareerStageRule
{
    [Required, MaxLength(50)]
    public string Stage { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Range(1, 100)]
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
    public List<string> Signals { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();
}

public class BenchmarkMetricConfig
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<string> GeographicLevels { get; set; } = new();

    [MaxLength(50)]
    public string? AggregationType { get; set; }
}

public class ValuationDriverConfig
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Range(-1, 1)]
    public decimal ImpactWeight { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class RegistrySourceConfig
{
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<string> TaxonomyCodes { get; set; } = new();

    [MaxLength(500)]
    public string? EndpointUrl { get; set; }
}

public class RoleDefinitionConfig
{
    [Required, MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsProvider { get; set; }

    public bool IsRequired { get; set; }
}

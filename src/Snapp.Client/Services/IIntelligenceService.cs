using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;

namespace Snapp.Client.Services;

public interface IIntelligenceService
{
    Task<DashboardResponse?> GetDashboardAsync();
    Task<ScoreResponse?> GetScoreAsync();
    Task<ContributionListResponse?> GetContributionsAsync();
    Task<MessageResponse?> ContributeDataAsync(SubmitDataRequest request);
    Task<VerticalConfigResponse?> GetVerticalConfigAsync();
    Task<BenchmarkResponse?> GetBenchmarksAsync(string specialty, string geography, string sizeBand);
    Task<ValuationResponse?> GetValuationAsync();
    Task<ValuationResponse?> ComputeScenarioAsync(Dictionary<string, string> overrides);
    Task<CareerStageResponse?> GetCareerStageAsync();
    Task<MarketProfileResponse?> GetMarketProfileAsync(string geoId);
    Task<MarketCompareResponse?> CompareMarketsAsync(string[] geoIds);
}

public class ScoreResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, decimal> DimensionScores { get; set; } = new();
    public decimal OverallScore { get; set; }
    public string ConfidenceLevel { get; set; } = "low";
    public DateTime ComputedAt { get; set; }
}

public class ContributionListResponse
{
    public List<ContributedCategory> Categories { get; set; } = new();
    public decimal TotalConfidence { get; set; }
}

public class ContributedCategory
{
    public string Category { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public int DataPointCount { get; set; }
    public DateTime LatestSubmission { get; set; }
}

public class VerticalConfigResponse
{
    public string Vertical { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<DimensionConfig> Dimensions { get; set; } = new();
    public List<ContributionCategoryConfig> ContributionCategories { get; set; } = new();
}

public class DimensionConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
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

public class CareerStageResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = "low";
    public List<RiskFlagResponse> RiskFlags { get; set; } = new();
    public List<string> TriggerSignals { get; set; } = new();
    public DateTime ComputedAt { get; set; }
}

public class RiskFlagResponse
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Description { get; set; } = string.Empty;
}

public class MarketProfileResponse
{
    public string GeoId { get; set; } = string.Empty;
    public string GeoName { get; set; } = string.Empty;
    public decimal PractitionerDensity { get; set; }
    public int CompetitorCount { get; set; }
    public decimal ConsolidationPressure { get; set; }
    public List<DemographicTrend> DemographicTrends { get; set; } = new();
    public List<WorkforceIndicator> WorkforceIndicators { get; set; } = new();
    public DateTime ComputedAt { get; set; }
}

public class DemographicTrend
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
}

public class WorkforceIndicator
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class MarketCompareResponse
{
    public List<MarketProfileResponse> Markets { get; set; } = new();
}

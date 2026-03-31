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

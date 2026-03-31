using Snapp.Service.Intelligence.Config;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Deterministic scoring engine that evaluates 6 configurable dimensions
/// based on contributed practice data and vertical configuration.
/// </summary>
public class ScoringEngine
{
    private readonly VerticalConfig _config;

    public ScoringEngine(VerticalConfig config) => _config = config;

    /// <summary>
    /// Computes a multi-dimensional score from available practice data.
    /// Each dimension scored 0-100 based on available signals.
    /// Enrichment signals (e.g., from job posting analysis) are merged with user contributions.
    /// </summary>
    public ScoringResult ComputeScore(List<PracticeData> contributions, List<PracticeData>? enrichmentSignals = null)
    {
        var allContributions = enrichmentSignals is { Count: > 0 }
            ? contributions.Concat(enrichmentSignals).ToList()
            : contributions;

        var dimensionScores = new Dictionary<string, decimal>();
        decimal weightedSum = 0;
        decimal totalWeight = 0;

        foreach (var dim in _config.Dimensions)
        {
            var dimContributions = allContributions
                .Where(c => c.Dimension == dim.Name)
                .ToList();

            var score = dim.Name == "Workforce"
                ? ScoreWorkforceDimension(dim, dimContributions)
                : ScoreDimension(dim, dimContributions);
            dimensionScores[dim.Name] = score;

            weightedSum += score * dim.Weight;
            totalWeight += dim.Weight;
        }

        var overallScore = totalWeight > 0 ? weightedSum / totalWeight : 0m;

        // Determine confidence level based on data coverage
        var coveredDimensions = dimensionScores.Count(kvp => kvp.Value > 0);
        var totalDimensions = _config.Dimensions.Count;
        var coverageRatio = totalDimensions > 0
            ? (decimal)coveredDimensions / totalDimensions
            : 0m;

        var confidenceLevel = coverageRatio switch
        {
            >= 0.8m => "high",
            >= 0.5m => "medium",
            _ => "low",
        };

        return new ScoringResult
        {
            DimensionScores = dimensionScores,
            OverallScore = Math.Round(overallScore, 2),
            ConfidenceLevel = confidenceLevel,
        };
    }

    /// <summary>
    /// Calculates the confidence contribution for a data submission in a given category.
    /// </summary>
    public decimal CalculateConfidenceContribution(string category)
    {
        var categoryConfig = _config.ContributionCategories
            .FirstOrDefault(c => c.Category == category);

        return categoryConfig?.ConfidenceWeight ?? 0m;
    }

    /// <summary>
    /// Calculates total confidence score from all contributed categories.
    /// Starts from baseConfidence, adds per-category weights, caps at maxConfidence.
    /// </summary>
    public decimal CalculateTotalConfidence(List<PracticeData> contributions)
    {
        var contributedCategories = contributions
            .Select(c => c.Category)
            .Distinct()
            .ToHashSet();

        var additionalConfidence = _config.ContributionCategories
            .Where(cc => contributedCategories.Contains(cc.Category))
            .Sum(cc => cc.ConfidenceWeight * 100);

        var total = _config.BaseConfidence + additionalConfidence;
        return Math.Min(total, _config.MaxConfidence);
    }

    /// <summary>
    /// Validates that a category is defined in the vertical configuration.
    /// </summary>
    public bool IsValidCategory(string category) =>
        _config.ContributionCategories.Any(c => c.Category == category);

    /// <summary>
    /// Gets the dimension name for a contribution category.
    /// </summary>
    public string? GetDimensionForCategory(string category) =>
        _config.ContributionCategories.FirstOrDefault(c => c.Category == category)?.Dimension;

    private static decimal ScoreDimension(DimensionConfig dim, List<PracticeData> contributions)
    {
        if (contributions.Count == 0)
            return 0m;

        // Score based on how many KPIs have data provided
        var kpiNames = dim.Kpis.Select(k => k.Name).ToHashSet();
        var providedDataPoints = contributions
            .SelectMany(c => c.DataPoints.Keys)
            .Distinct()
            .Count(dp => kpiNames.Contains(dp));

        if (providedDataPoints == 0)
            return 0m;

        var coverageScore = kpiNames.Count > 0
            ? (decimal)providedDataPoints / kpiNames.Count * 100
            : 0m;

        // Evaluate actual values where we can parse them as decimals
        var valueScores = new List<decimal>();
        foreach (var kpi in dim.Kpis)
        {
            var dataPoint = contributions
                .SelectMany(c => c.DataPoints)
                .FirstOrDefault(dp => dp.Key == kpi.Name);

            if (dataPoint.Key is null)
                continue;

            if (decimal.TryParse(dataPoint.Value, out var value))
            {
                // Normalize: score based on thresholds
                var score = NormalizeValue(value, dim.Thresholds, kpi.Unit);
                valueScores.Add(score);
            }
            else
            {
                // Boolean or non-numeric — if present, credit half
                valueScores.Add(50m);
            }
        }

        if (valueScores.Count > 0)
            return Math.Round(valueScores.Average(), 2);

        return Math.Round(coverageScore, 2);
    }

    /// <summary>
    /// Scores the Workforce dimension using inverse logic: lower pressure = better score.
    /// WorkforcePressureScore is 0-100 where 100 = maximum pressure (bad).
    /// We invert: stability = 100 - pressure.
    /// </summary>
    private static decimal ScoreWorkforceDimension(DimensionConfig dim, List<PracticeData> contributions)
    {
        if (contributions.Count == 0)
            return 0m;

        var dataPoints = contributions
            .SelectMany(c => c.DataPoints)
            .GroupBy(dp => dp.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        var scores = new List<decimal>();

        // WorkforcePressureScore: invert (low pressure = high stability score)
        if (dataPoints.TryGetValue("WorkforcePressureScore", out var pressureStr)
            && decimal.TryParse(pressureStr, out var pressure))
        {
            scores.Add(Math.Max(0, 100m - pressure));
        }

        // PostingFrequency: lower = better. Cap at 12 postings/year as worst case
        if (dataPoints.TryGetValue("PostingFrequency", out var freqStr)
            && decimal.TryParse(freqStr, out var freq))
        {
            var normalized = Math.Max(0, 100m - freq / 12m * 100m);
            scores.Add(Math.Max(0, normalized));
        }

        // ChronicTurnoverSignal: boolean — true = 0 (bad), false = 100 (good)
        if (dataPoints.TryGetValue("ChronicTurnoverSignal", out var chronicStr))
        {
            scores.Add(bool.TryParse(chronicStr, out var chronic) && chronic ? 0m : 100m);
        }

        // UrgentPostingRatio: lower = better (invert percentage)
        if (dataPoints.TryGetValue("UrgentPostingRatio", out var urgentStr)
            && decimal.TryParse(urgentStr, out var urgentRatio))
        {
            scores.Add(Math.Max(0, 100m - urgentRatio));
        }

        return scores.Count > 0 ? Math.Round(scores.Average(), 2) : 0m;
    }

    private static decimal NormalizeValue(decimal value, Dictionary<string, decimal> thresholds, string unit)
    {
        // For percentage-based metrics, the value is already 0-100 ish
        if (unit == "%" && value >= 0 && value <= 100)
        {
            return value;
        }

        // Use thresholds to determine score band
        var strong = thresholds.GetValueOrDefault("strong", 75);
        var acceptable = thresholds.GetValueOrDefault("acceptable", 50);
        var weak = thresholds.GetValueOrDefault("weak", 25);

        if (value >= strong) return Math.Min(100, 75 + (value - strong) / 2);
        if (value >= acceptable) return 50 + (value - acceptable) / (strong - acceptable) * 25;
        if (value >= weak) return 25 + (value - weak) / (acceptable - weak) * 25;
        return Math.Max(0, value / weak * 25);
    }
}

public class ScoringResult
{
    public Dictionary<string, decimal> DimensionScores { get; set; } = new();
    public decimal OverallScore { get; set; }
    public string ConfidenceLevel { get; set; } = "low";
}

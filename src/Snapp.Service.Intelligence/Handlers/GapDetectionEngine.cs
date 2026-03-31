using Snapp.Service.Intelligence.Config;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Analyzes a user's intelligence profile for gaps (missing data categories,
/// unconfirmed public signals, low-confidence items) and generates prioritized
/// micro-questions to fill those gaps.
/// </summary>
public class GapDetectionEngine
{
    private readonly VerticalConfig _config;

    public GapDetectionEngine(VerticalConfig config) => _config = config;

    /// <summary>
    /// Detects intelligence gaps and generates prioritized questions.
    /// Priority = gap_weight × answer_ease × unlock_value.
    /// </summary>
    public List<GeneratedQuestion> DetectGapsAndGenerateQuestions(
        string userId,
        List<PracticeData> existingData,
        decimal currentConfidence)
    {
        var contributedCategories = existingData
            .Select(d => d.Category)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var contributedKpis = existingData
            .SelectMany(d => d.DataPoints.Keys)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var questions = new List<GeneratedQuestion>();

        // 1. Generate confirm_data questions for existing data that could be stale
        foreach (var data in existingData)
        {
            if (data.Source == "public" || data.Source == "enrichment")
            {
                foreach (var dp in data.DataPoints)
                {
                    questions.Add(new GeneratedQuestion
                    {
                        UserId = userId,
                        Type = QuestionType.ConfirmData,
                        Category = data.Category,
                        Dimension = data.Dimension,
                        PromptText = BuildConfirmDataPrompt(dp.Key, dp.Value, data.Category),
                        Choices = new List<string> { "Yes", "No" },
                        UnlockDescription = $"Confirming this increases your {data.Dimension} confidence score.",
                        GapWeight = 0.6m,
                        AnswerEase = 0.9m,
                        UnlockValue = 0.5m,
                        RelatedDataPoint = dp.Key,
                        RelatedValue = dp.Value,
                    });
                }
            }
        }

        // 2. Generate estimate_value questions for missing high-value categories
        foreach (var catConfig in _config.ContributionCategories)
        {
            if (contributedCategories.Contains(catConfig.Category))
                continue;

            var dimension = _config.Dimensions.FirstOrDefault(d => d.Name == catConfig.Dimension);
            if (dimension is null) continue;

            // Find the first missing KPI in this category
            var missingKpi = dimension.Kpis
                .Where(k => k.Category == catConfig.Category)
                .FirstOrDefault(k => !contributedKpis.Contains(k.Name));

            if (missingKpi is null) continue;

            var gapWeight = catConfig.ConfidenceWeight > 0 ? Math.Min(1m, catConfig.ConfidenceWeight * 10) : 0.5m;

            questions.Add(new GeneratedQuestion
            {
                UserId = userId,
                Type = QuestionType.EstimateValue,
                Category = catConfig.Category,
                Dimension = catConfig.Dimension,
                PromptText = BuildEstimatePrompt(missingKpi),
                Choices = BuildEstimateChoices(missingKpi),
                UnlockDescription = $"Answering unlocks your {catConfig.DisplayName} benchmarks and improves your confidence score by {catConfig.ConfidenceWeight * 100:F0}%.",
                GapWeight = gapWeight,
                AnswerEase = 0.7m,
                UnlockValue = catConfig.ConfidenceWeight > 0 ? Math.Min(1m, catConfig.ConfidenceWeight * 8) : 0.4m,
                RelatedDataPoint = missingKpi.Name,
            });
        }

        // 3. Generate confirm_relationship questions (simulated — in production these
        //    come from co-location analysis and network membership)
        if (currentConfidence < 70)
        {
            questions.Add(new GeneratedQuestion
            {
                UserId = userId,
                Type = QuestionType.ConfirmRelationship,
                Category = "relationships",
                Dimension = "MarketPosition",
                PromptText = "Do you know other practitioners in your area that you would refer patients to?",
                Choices = new List<string> { "Yes", "No" },
                UnlockDescription = "Confirming relationships builds your referral network and unlocks peer comparisons.",
                GapWeight = 0.4m,
                AnswerEase = 0.8m,
                UnlockValue = 0.6m,
            });
        }

        // 4. If user has some data but missing KPIs within contributed categories
        foreach (var catConfig in _config.ContributionCategories)
        {
            if (!contributedCategories.Contains(catConfig.Category))
                continue;

            var dimension = _config.Dimensions.FirstOrDefault(d => d.Name == catConfig.Dimension);
            if (dimension is null) continue;

            var missingKpis = dimension.Kpis
                .Where(k => k.Category == catConfig.Category && !contributedKpis.Contains(k.Name))
                .ToList();

            foreach (var kpi in missingKpis)
            {
                questions.Add(new GeneratedQuestion
                {
                    UserId = userId,
                    Type = QuestionType.EstimateValue,
                    Category = catConfig.Category,
                    Dimension = catConfig.Dimension,
                    PromptText = BuildEstimatePrompt(kpi),
                    Choices = BuildEstimateChoices(kpi),
                    UnlockDescription = $"Adding {kpi.DisplayName} improves your {dimension.DisplayName} score accuracy.",
                    GapWeight = 0.5m,
                    AnswerEase = 0.7m,
                    UnlockValue = 0.3m,
                    RelatedDataPoint = kpi.Name,
                });
            }
        }

        // Sort by priority (gap_weight × answer_ease × unlock_value) descending
        return questions
            .OrderByDescending(q => q.Priority)
            .ToList();
    }

    private static string BuildConfirmDataPrompt(string dataPoint, string value, string category) =>
        dataPoint switch
        {
            "Address" or "PracticeAddress" => $"We see you're at {value} — is this current?",
            "AnnualRevenue" => $"Is your annual revenue still around ${decimal.Parse(value):N0}?",
            "ProviderCount" => $"Do you still have {value} providers?",
            "OwnerProductionPct" => $"Is your owner production still around {value}%?",
            _ => $"We have your {FormatDataPointName(dataPoint)} recorded as {value} — is this still accurate?",
        };

    private static string BuildEstimatePrompt(KpiConfig kpi) =>
        kpi.Unit switch
        {
            "USD" => $"Is your {kpi.DisplayName} closer to...",
            "%" => $"What is your approximate {kpi.DisplayName}?",
            "count" => $"Approximately how many: {kpi.DisplayName}?",
            "bool" => $"Do you have a {kpi.DisplayName}?",
            _ => $"What is your approximate {kpi.DisplayName}?",
        };

    private static List<string> BuildEstimateChoices(KpiConfig kpi) =>
        kpi.Unit switch
        {
            "USD" when kpi.Name.Contains("Revenue", StringComparison.OrdinalIgnoreCase) =>
                new() { "Under $500K", "$500K - $750K", "$750K - $1M", "$1M - $1.5M", "Over $1.5M" },
            "USD" =>
                new() { "Under $100K", "$100K - $250K", "$250K - $500K", "$500K - $1M", "Over $1M" },
            "%" =>
                new() { "Under 25%", "25% - 50%", "50% - 75%", "Over 75%" },
            "count" =>
                new() { "0", "1-2", "3-5", "6-10", "More than 10" },
            "bool" =>
                new() { "Yes", "No" },
            "ratio" =>
                new() { "Less than 2:1", "2:1 - 3:1", "3:1 - 4:1", "More than 4:1" },
            "days" =>
                new() { "1-2", "3-4", "5+", },
            _ =>
                new() { "Low", "Medium", "High" },
        };

    private static string FormatDataPointName(string name)
    {
        // Convert PascalCase to words: "AnnualRevenue" → "annual revenue"
        var result = string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLower(c) : char.ToLower(c).ToString()));
        return result;
    }
}

public enum QuestionType
{
    ConfirmData,
    ConfirmRelationship,
    EstimateValue,
}

public class GeneratedQuestion
{
    public string UserId { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public string UnlockDescription { get; set; } = string.Empty;
    public decimal GapWeight { get; set; }
    public decimal AnswerEase { get; set; }
    public decimal UnlockValue { get; set; }
    public string? RelatedDataPoint { get; set; }
    public string? RelatedValue { get; set; }

    public decimal Priority => GapWeight * AnswerEase * UnlockValue;
}

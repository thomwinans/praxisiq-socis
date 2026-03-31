using Snapp.Service.Intelligence.Config;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Deterministic career stage classifier using rules from the Vertical Config Pack.
/// Evaluates tenure, co-location, production volume, entity type, and reputation
/// signals to classify practitioners into one of 6 career stages.
/// </summary>
public class CareerStageClassifier
{
    private readonly VerticalConfig _config;

    public CareerStageClassifier(VerticalConfig config) => _config = config;

    public CareerStageResult Classify(CareerStageInput input)
    {
        // Incorporate license tenure: if license tenure is available, use the
        // maximum of explicit tenure and license-derived tenure as the effective
        // tenure for classification. License issue dates are an authoritative
        // signal for how long a provider has been practicing.
        var effectiveInput = input;
        if (input.LicenseTenureYears > 0)
        {
            effectiveInput = input with
            {
                TenureYears = Math.Max(input.TenureYears, input.LicenseTenureYears),
            };
        }

        var rules = _config.CareerStageRules
            .OrderBy(r => r.Priority)
            .ToList();

        string matchedStage = "TrainingEntry";
        string matchedDisplayName = "Training / Entry";
        var triggerSignals = new List<string>();

        foreach (var rule in rules)
        {
            var (matches, signals) = EvaluateRule(rule, effectiveInput);
            if (matches)
            {
                matchedStage = rule.Stage;
                matchedDisplayName = rule.DisplayName;
                triggerSignals = signals;
                break;
            }
        }

        // If no rule matched, default to TrainingEntry
        if (triggerSignals.Count == 0)
            triggerSignals.Add("default_classification");

        // Record license tenure as a trigger signal when it influenced classification
        if (input.LicenseTenureYears > 0 && input.LicenseTenureYears > input.TenureYears)
            triggerSignals.Add($"licenseTenure={input.LicenseTenureYears:F1}y");

        var riskFlags = ComputeRiskFlags(matchedStage, input);
        var confidence = ComputeConfidence(input);

        return new CareerStageResult
        {
            Stage = matchedStage,
            DisplayName = matchedDisplayName,
            ConfidenceLevel = confidence,
            RiskFlags = riskFlags,
            TriggerSignals = triggerSignals,
        };
    }

    private static (bool Matches, List<string> Signals) EvaluateRule(CareerStageRule rule, CareerStageInput input)
    {
        var signals = new List<string>();

        // Tenure checks
        if (rule.MinTenureYears.HasValue)
        {
            if (input.TenureYears < rule.MinTenureYears.Value) return (false, signals);
            signals.Add($"tenure>={rule.MinTenureYears.Value}y");
        }
        if (rule.MaxTenureYears.HasValue)
        {
            if (input.TenureYears > rule.MaxTenureYears.Value) return (false, signals);
            signals.Add($"tenure<={rule.MaxTenureYears.Value}y");
        }

        // Provider count checks
        if (rule.MinProviderCount.HasValue)
        {
            if (input.ProviderCount < rule.MinProviderCount.Value) return (false, signals);
            signals.Add($"providers>={rule.MinProviderCount.Value}");
        }
        if (rule.MaxProviderCount.HasValue)
        {
            if (input.ProviderCount > rule.MaxProviderCount.Value) return (false, signals);
            signals.Add($"providers<={rule.MaxProviderCount.Value}");
        }

        // Location count check
        if (rule.MinLocationCount.HasValue)
        {
            if (input.LocationCount < rule.MinLocationCount.Value) return (false, signals);
            signals.Add($"locations>={rule.MinLocationCount.Value}");
        }

        // Production volume check
        if (rule.MinProductionVolume.HasValue)
        {
            if (input.ProductionVolume < rule.MinProductionVolume.Value) return (false, signals);
            signals.Add($"production>={rule.MinProductionVolume.Value}");
        }

        // Owner production percentage checks
        if (rule.MaxOwnerProductionPct.HasValue)
        {
            if (input.OwnerProductionPct > rule.MaxOwnerProductionPct.Value) return (false, signals);
            signals.Add($"ownerProd<={rule.MaxOwnerProductionPct.Value}%");
        }
        if (rule.MinOwnerProductionPct.HasValue)
        {
            if (input.OwnerProductionPct < rule.MinOwnerProductionPct.Value) return (false, signals);
            signals.Add($"ownerProd>={rule.MinOwnerProductionPct.Value}%");
        }

        // Co-location check
        if (rule.RequiresCoLocation == true)
        {
            if (input.CoLocationCount <= 0) return (false, signals);
            signals.Add($"coLocated={input.CoLocationCount}");
        }

        // Entity formation check
        if (rule.RequiresEntityFormation == true)
        {
            if (!input.HasEntityFormation) return (false, signals);
            signals.Add("entity_formed");
        }

        // Succession plan check
        if (rule.RequiresSuccessionPlan == true)
        {
            if (!input.HasSuccessionPlan) return (false, signals);
            signals.Add("succession_plan_exists");
        }

        // CE hours check (low CE hours = Pre-Exit signal)
        if (rule.MaxCeHoursRecent.HasValue)
        {
            if (input.CeHoursRecent > rule.MaxCeHoursRecent.Value) return (false, signals);
            signals.Add($"ceHours<={rule.MaxCeHoursRecent.Value}");
        }

        // Entity type check (empty list = any type accepted)
        if (rule.EntityTypes.Count > 0)
        {
            if (string.IsNullOrEmpty(input.EntityType) || !rule.EntityTypes.Contains(input.EntityType, StringComparer.OrdinalIgnoreCase))
                return (false, signals);
            signals.Add($"entityType={input.EntityType}");
        }

        return (signals.Count > 0, signals);
    }

    private static List<RiskFlag> ComputeRiskFlags(string stage, CareerStageInput input)
    {
        var flags = new List<RiskFlag>();

        // Retirement risk: Pre-Exit stage or very long tenure
        if (stage == "PreExit" || input.TenureYears >= 25)
        {
            flags.Add(new RiskFlag
            {
                Type = "retirement_risk",
                Severity = input.TenureYears >= 30 ? "high" : "medium",
                Description = "Extended tenure with declining engagement signals potential retirement.",
            });
        }

        // Succession risk: No succession plan + high owner dependency
        if (!input.HasSuccessionPlan && input.OwnerProductionPct >= 70)
        {
            flags.Add(new RiskFlag
            {
                Type = "succession_risk",
                Severity = input.OwnerProductionPct >= 85 ? "high" : "medium",
                Description = "No succession plan with high owner production dependency.",
            });
        }

        // Overextension: Growth stage + rapid expansion signals
        if (stage == "Growth" && input.LocationCount >= 3 && input.ProviderCount >= 5)
        {
            flags.Add(new RiskFlag
            {
                Type = "overextension",
                Severity = input.LocationCount >= 5 ? "high" : "medium",
                Description = "Rapid expansion across multiple locations may indicate overextension.",
            });
        }

        // Key-person dependency: High owner production % + low provider count
        if (input.OwnerProductionPct >= 80 && input.ProviderCount <= 1)
        {
            flags.Add(new RiskFlag
            {
                Type = "key_person_dependency",
                Severity = input.OwnerProductionPct >= 90 ? "high" : "medium",
                Description = "Practice relies heavily on single provider with limited backup.",
            });
        }

        return flags;
    }

    private static string ComputeConfidence(CareerStageInput input)
    {
        int signalCount = 0;
        int totalSignals = 9; // tenure, coLocation, production, entity, providers, locations, ownerProd, ceHours, licenseTenure

        if (input.TenureYears > 0) signalCount++;
        if (input.CoLocationCount >= 0) signalCount++; // 0 is valid (not co-located)
        if (input.ProductionVolume > 0) signalCount++;
        if (!string.IsNullOrEmpty(input.EntityType)) signalCount++;
        if (input.ProviderCount > 0) signalCount++;
        if (input.LocationCount > 0) signalCount++;
        if (input.OwnerProductionPct > 0) signalCount++;
        if (input.CeHoursRecent >= 0) signalCount++;
        if (input.LicenseTenureYears > 0) signalCount++;

        var ratio = (decimal)signalCount / totalSignals;
        return ratio switch
        {
            >= 0.75m => "high",
            >= 0.5m => "medium",
            _ => "low",
        };
    }
}

public record CareerStageInput
{
    public decimal TenureYears { get; set; }
    public int CoLocationCount { get; set; }
    public decimal ProductionVolume { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int ProviderCount { get; set; }
    public int LocationCount { get; set; }
    public decimal OwnerProductionPct { get; set; }
    public bool HasSuccessionPlan { get; set; }
    public bool HasEntityFormation { get; set; }
    public decimal CeHoursRecent { get; set; }
    public decimal ReputationScore { get; set; }
    public decimal LicenseTenureYears { get; set; }
}

public class CareerStageResult
{
    public string Stage { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = "low";
    public List<RiskFlag> RiskFlags { get; set; } = new();
    public List<string> TriggerSignals { get; set; } = new();
}

public class RiskFlag
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Description { get; set; } = string.Empty;
}

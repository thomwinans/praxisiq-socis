using Snapp.Service.Intelligence.Config;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Three-case valuation engine: Revenue × EBITDA margin × Multiple.
/// Calibrated against benchmarks. Confidence from data completeness.
/// Drivers identified from vertical configuration.
/// </summary>
public class ValuationEngine
{
    private readonly VerticalConfig _config;
    private readonly ScoringEngine _scoringEngine;

    public ValuationEngine(VerticalConfig config, ScoringEngine scoringEngine)
    {
        _config = config;
        _scoringEngine = scoringEngine;
    }

    public ValuationResult Compute(List<PracticeData> contributions, List<Benchmark> benchmarks,
        Dictionary<string, string>? overrides = null)
    {
        var data = MergeDataPoints(contributions, overrides);

        var revenue = ResolveDecimal(data, "AnnualRevenue")
            ?? ResolveBenchmarkP50(benchmarks, "AnnualRevenue")
            ?? 0m;

        var ebitdaMargin = ResolveDecimal(data, "ProfitMargin") / 100m
            ?? ResolveDecimal(data, "EbitdaMargin") / 100m
            ?? ResolveBenchmarkP50Pct(benchmarks, "ProfitMargin")
            ?? ResolveBenchmarkP50Pct(benchmarks, "OverheadRatio", invert: true)
            ?? 0.20m; // conservative fallback

        var multiple = ResolveMultiple(data, benchmarks);

        // Three-case model per TRD WU-4.3
        var baseVal = revenue * ebitdaMargin * multiple;
        var downside = revenue * (ebitdaMargin - 0.05m) * (multiple - 0.5m);
        var upside = revenue * (ebitdaMargin + 0.05m) * (multiple + 0.5m);

        // Floor at zero
        downside = Math.Max(0, downside);
        baseVal = Math.Max(0, baseVal);
        upside = Math.Max(0, upside);

        var confidence = _scoringEngine.CalculateTotalConfidence(contributions);
        var drivers = IdentifyDrivers(data, benchmarks);

        return new ValuationResult
        {
            Downside = Math.Round(downside, 2),
            Base = Math.Round(baseVal, 2),
            Upside = Math.Round(upside, 2),
            ConfidenceScore = confidence,
            Multiple = multiple,
            EbitdaMargin = ebitdaMargin,
            Drivers = drivers,
        };
    }

    /// <summary>
    /// Determines if a valuation changed significantly (>5%) from a previous one.
    /// </summary>
    public static bool IsSignificantChange(Valuation? previous, decimal newBase)
    {
        if (previous is null || previous.Base == 0) return false;
        var pctChange = Math.Abs((newBase - previous.Base) / previous.Base);
        return pctChange > 0.05m;
    }

    private static Dictionary<string, string> MergeDataPoints(
        List<PracticeData> contributions, Dictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in contributions)
        {
            foreach (var dp in c.DataPoints)
                merged[dp.Key] = dp.Value;
        }

        if (overrides is not null)
        {
            foreach (var o in overrides)
                merged[o.Key] = o.Value;
        }

        return merged;
    }

    private static decimal? ResolveDecimal(Dictionary<string, string> data, string key)
    {
        if (data.TryGetValue(key, out var val) && decimal.TryParse(val, out var d))
            return d;
        return null;
    }

    private static decimal? ResolveBenchmarkP50(List<Benchmark> benchmarks, string metricName)
    {
        var bm = benchmarks.FirstOrDefault(b =>
            string.Equals(b.MetricName, metricName, StringComparison.OrdinalIgnoreCase));
        return bm?.P50 > 0 ? bm.P50 : null;
    }

    private static decimal? ResolveBenchmarkP50Pct(List<Benchmark> benchmarks, string metricName, bool invert = false)
    {
        var p50 = ResolveBenchmarkP50(benchmarks, metricName);
        if (p50 is null) return null;
        var pct = p50.Value / 100m;
        return invert ? 1m - pct : pct;
    }

    private static decimal ResolveMultiple(Dictionary<string, string> data, List<Benchmark> benchmarks)
    {
        // Base multiple for dental practices: ~4-7x EBITDA
        // Adjusted by owner dependency and size
        var baseMultiple = 5.0m;

        // Owner production % suppresses multiple (key-person risk)
        var ownerPct = ResolveDecimal(data, "OwnerProductionPct");
        if (ownerPct is not null)
        {
            if (ownerPct > 80) baseMultiple -= 1.5m;
            else if (ownerPct > 60) baseMultiple -= 0.75m;
            else if (ownerPct < 30) baseMultiple += 0.5m;
        }

        // Provider count: more providers = more diversified = higher multiple
        var providerCount = ResolveDecimal(data, "ProviderCount");
        if (providerCount is not null && providerCount > 3)
            baseMultiple += 0.5m;

        // Succession plan: positive signal
        if (data.TryGetValue("SuccessionPlanExists", out var spVal) &&
            (spVal == "true" || spVal == "1" || spVal == "yes"))
            baseMultiple += 0.25m;

        return Math.Max(2.0m, Math.Min(8.0m, baseMultiple));
    }

    private Dictionary<string, string> IdentifyDrivers(
        Dictionary<string, string> data, List<Benchmark> benchmarks)
    {
        var drivers = new Dictionary<string, string>();

        // Key-person / owner dependency
        var ownerPct = ResolveDecimal(data, "OwnerProductionPct");
        if (ownerPct is not null)
        {
            if (ownerPct > 70)
                drivers["OwnerDependency"] = $"High owner production ({ownerPct:F0}%) suppresses multiple. This is the single largest valuation lever for most practices.";
            else if (ownerPct < 40)
                drivers["OwnerDependency"] = $"Low owner production ({ownerPct:F0}%) supports a higher multiple — practice is less dependent on a single provider.";
        }
        else
        {
            drivers["OwnerDependency"] = "Owner production % not provided. This is typically the largest driver — contributing this data will significantly improve confidence.";
        }

        // Revenue diversification
        var topPayer = ResolveDecimal(data, "TopPayerConcentration");
        if (topPayer is not null && topPayer > 40)
            drivers["PayerConcentration"] = $"Top payer represents {topPayer:F0}% of revenue — diversifying payer mix would reduce risk.";

        // Staff stability
        var staffRatio = ResolveDecimal(data, "StaffToProviderRatio");
        if (staffRatio is not null && staffRatio < 3)
            drivers["StaffDepth"] = "Staff-to-provider ratio is below typical thresholds, indicating potential operational strain.";

        // Market dynamics
        var dsoPresence = data.TryGetValue("DsoPresence", out var dsoVal) &&
            (dsoVal == "true" || dsoVal == "1" || dsoVal == "yes");
        var popGrowth = ResolveDecimal(data, "PopulationGrowthRate");
        if (dsoPresence)
            drivers["MarketDynamics"] = "DSO presence in area indicates consolidation pressure — may affect future standalone valuation.";
        else if (popGrowth is not null && popGrowth > 2)
            drivers["MarketDynamics"] = $"Strong population growth ({popGrowth:F1}%) supports revenue sustainability.";

        // Facility / lease
        var revenue = ResolveDecimal(data, "AnnualRevenue");
        var overhead = ResolveDecimal(data, "OverheadRatio");
        if (revenue is not null && overhead is not null && overhead > 70)
            drivers["OverheadRisk"] = $"Overhead ratio at {overhead:F0}% is elevated — facility/lease costs may be compressing margins.";

        return drivers;
    }
}

public class ValuationResult
{
    public decimal Downside { get; set; }
    public decimal Base { get; set; }
    public decimal Upside { get; set; }
    public decimal ConfidenceScore { get; set; }
    public decimal Multiple { get; set; }
    public decimal EbitdaMargin { get; set; }
    public Dictionary<string, string> Drivers { get; set; } = new();
}

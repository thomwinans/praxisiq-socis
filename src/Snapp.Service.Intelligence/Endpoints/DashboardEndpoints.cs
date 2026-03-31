using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/intel/dashboard", HandleGetDashboard)
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .Produces<DashboardResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetDashboard(
        HttpRequest request,
        IntelligenceRepository repo,
        ScoringEngine engine)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        // Get all user's contributed data
        var contributions = await repo.GetUserDataAsync(userId);
        var totalConfidence = engine.CalculateTotalConfidence(contributions);

        // Build KPIs from available data
        var kpis = BuildKpis(contributions);

        // Get valuation summary if it exists
        var valuation = await repo.GetCurrentValuationAsync(userId);
        ValuationSummary? valuationSummary = null;
        if (valuation is not null)
        {
            valuationSummary = new ValuationSummary
            {
                Downside = valuation.Downside,
                Base = valuation.Base,
                Upside = valuation.Upside,
                ConfidenceScore = valuation.ConfidenceScore,
            };
        }

        return Results.Ok(new DashboardResponse
        {
            KPIs = kpis,
            ConfidenceScore = totalConfidence,
            ValuationSummary = valuationSummary,
        });
    }

    private static List<KpiItem> BuildKpis(List<PracticeData> contributions)
    {
        var kpis = new List<KpiItem>();

        // Extract KPIs from contributed data
        var allDataPoints = contributions
            .SelectMany(c => c.DataPoints)
            .GroupBy(dp => dp.Key)
            .ToDictionary(g => g.Key, g => g.Last().Value);

        foreach (var (name, value) in allDataPoints)
        {
            var unit = InferUnit(name);
            kpis.Add(new KpiItem
            {
                Name = FormatKpiName(name),
                Value = value,
                Unit = unit,
                Trend = "Flat",
            });
        }

        return kpis;
    }

    private static string FormatKpiName(string name)
    {
        // Convert PascalCase to Title Case
        var result = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }

    private static string? InferUnit(string name)
    {
        if (name.Contains("Pct", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Ratio", StringComparison.OrdinalIgnoreCase))
            return "%";

        if (name.Contains("Revenue", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Margin", StringComparison.OrdinalIgnoreCase))
            return "USD";

        if (name.Contains("Count", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Patients", StringComparison.OrdinalIgnoreCase))
            return "count";

        return null;
    }
}

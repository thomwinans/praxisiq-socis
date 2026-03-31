using Amazon.DynamoDBv2;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.Shared.Enums;
using Snapp.Shared.Hosting;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Endpoints;

public static class ValuationEndpoints
{
    public static void MapValuationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/intel/valuation", HandleGetValuation)
            .WithName("GetCurrentValuation")
            .WithTags("Valuation")
            .Produces<ValuationResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/intel/valuation/compute", HandleComputeValuation)
            .WithName("ComputeValuation")
            .WithTags("Valuation")
            .Produces<ValuationResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/intel/valuation/scenario", HandleScenario)
            .WithName("ComputeValuationScenario")
            .WithTags("Valuation")
            .Accepts<ScenarioRequest>("application/json")
            .Produces<ValuationResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetValuation(
        HttpRequest request,
        IntelligenceRepository repo,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var current = await repo.GetCurrentValuationAsync(userId);
        if (current is null)
            return EndpointHelpers.NotFound(traceId, ErrorCodes.ValuationNotFound,
                "No valuation found. Compute a valuation first.");

        var history = await repo.GetValuationHistoryAsync(userId, 12);

        logger.LogInformation(
            "Valuation retrieved userId={UserId}, base={Base}, traceId={TraceId}",
            userId, current.Base, traceId);

        return Results.Ok(ToResponse(current, history));
    }

    private static async Task<IResult> HandleComputeValuation(
        HttpRequest request,
        IntelligenceRepository repo,
        ValuationEngine engine,
        IAmazonDynamoDB db,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var contributions = await repo.GetUserDataAsync(userId);
        var benchmarks = await repo.GetBenchmarksAsync("general-dentistry", "national", "small");

        var previous = await repo.GetCurrentValuationAsync(userId);
        var result = engine.Compute(contributions, benchmarks);

        var valuation = new Valuation
        {
            UserId = userId,
            Downside = result.Downside,
            Base = result.Base,
            Upside = result.Upside,
            ConfidenceScore = result.ConfidenceScore,
            Drivers = result.Drivers,
            Multiple = result.Multiple,
            EbitdaMargin = result.EbitdaMargin,
            ComputedAt = DateTime.UtcNow,
        };

        await repo.SaveValuationAsync(valuation);

        // Queue notification if significant change (>5%)
        if (ValuationEngine.IsSignificantChange(previous, result.Base))
        {
            var pctChange = previous!.Base > 0
                ? (result.Base - previous.Base) / previous.Base * 100
                : 0;
            var direction = pctChange > 0 ? "increased" : "decreased";

            await NotificationHelper.CreateNotificationAsync(
                db, userId, NotificationType.ValuationChanged,
                "Valuation Updated",
                $"Your practice valuation has {direction} by {Math.Abs(pctChange):F1}%. " +
                $"New base estimate: ${result.Base:N0}.",
                NotificationHelper.GetCategory(NotificationType.ValuationChanged),
                $"VAL#{userId}");
        }

        var history = await repo.GetValuationHistoryAsync(userId, 12);

        logger.LogInformation(
            "Valuation computed userId={UserId}, base={Base}, confidence={Confidence}, traceId={TraceId}",
            userId, result.Base, result.ConfidenceScore, traceId);

        return Results.Ok(ToResponse(valuation, history));
    }

    private static async Task<IResult> HandleScenario(
        HttpRequest request,
        ScenarioRequest body,
        IntelligenceRepository repo,
        ValuationEngine engine,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (body.Overrides is null || body.Overrides.Count == 0)
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed,
                "Overrides must contain at least one key-value pair.");

        var contributions = await repo.GetUserDataAsync(userId);
        var benchmarks = await repo.GetBenchmarksAsync("general-dentistry", "national", "small");

        var result = engine.Compute(contributions, benchmarks, body.Overrides);

        logger.LogInformation(
            "Valuation scenario computed userId={UserId}, base={Base}, overrides={Overrides}, traceId={TraceId}",
            userId, result.Base, body.Overrides.Count, traceId);

        // Scenario results are NOT saved
        return Results.Ok(new ValuationResponse
        {
            Downside = result.Downside,
            Base = result.Base,
            Upside = result.Upside,
            ConfidenceScore = result.ConfidenceScore,
            Drivers = result.Drivers.Select(d => new ValuationDriver
            {
                Name = d.Key,
                Impact = d.Value,
                Direction = DetermineDirection(d.Key, d.Value),
            }).ToList(),
            History = new List<ValuationSnapshot>(),
        });
    }

    private static ValuationResponse ToResponse(Valuation valuation, List<Valuation> history)
    {
        return new ValuationResponse
        {
            Downside = valuation.Downside,
            Base = valuation.Base,
            Upside = valuation.Upside,
            ConfidenceScore = valuation.ConfidenceScore,
            Drivers = valuation.Drivers.Select(d => new ValuationDriver
            {
                Name = d.Key,
                Impact = d.Value,
                Direction = DetermineDirection(d.Key, d.Value),
            }).ToList(),
            History = history.Select(h => new ValuationSnapshot
            {
                Date = h.ComputedAt,
                Base = h.Base,
                ConfidenceScore = h.ConfidenceScore,
            }).ToList(),
        };
    }

    private static string DetermineDirection(string driverName, string impact)
    {
        var lower = impact.ToLowerInvariant();
        if (lower.Contains("suppress") || lower.Contains("risk") || lower.Contains("elevated") ||
            lower.Contains("reduce") || lower.Contains("strain") || lower.Contains("compressing") ||
            lower.Contains("consolidation"))
            return "negative";
        if (lower.Contains("support") || lower.Contains("higher") || lower.Contains("strong") ||
            lower.Contains("low owner"))
            return "positive";
        return "neutral";
    }
}

public class ScenarioRequest
{
    public Dictionary<string, string> Overrides { get; set; } = new();
}

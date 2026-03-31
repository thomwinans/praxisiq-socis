using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Service.Intelligence.Endpoints;

public static class CareerStageEndpoints
{
    public static void MapCareerStageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/intel/career-stage/compute", HandleComputeCareerStage)
            .WithName("ComputeCareerStage")
            .WithTags("CareerStage")
            .Accepts<CareerStageRequest>("application/json")
            .Produces<CareerStageResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/intel/career-stage", HandleGetCareerStage)
            .WithName("GetCurrentCareerStage")
            .WithTags("CareerStage")
            .Produces<CareerStageResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/intel/career-stage/history", HandleGetCareerStageHistory)
            .WithName("GetCareerStageHistory")
            .WithTags("CareerStage")
            .Produces<CareerStageHistoryResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleComputeCareerStage(
        CareerStageRequest body,
        HttpRequest request,
        IntelligenceRepository repo,
        CareerStageClassifier classifier,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (body.TenureYears < 0)
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "TenureYears must be non-negative.");

        var input = new CareerStageInput
        {
            TenureYears = body.TenureYears,
            CoLocationCount = body.CoLocationCount,
            ProductionVolume = body.ProductionVolume,
            EntityType = body.EntityType ?? string.Empty,
            ProviderCount = body.ProviderCount,
            LocationCount = body.LocationCount,
            OwnerProductionPct = body.OwnerProductionPct,
            HasSuccessionPlan = body.HasSuccessionPlan,
            HasEntityFormation = body.HasEntityFormation,
            CeHoursRecent = body.CeHoursRecent,
            ReputationScore = body.ReputationScore,
        };

        var result = classifier.Classify(input);

        await repo.SaveCareerStageAsync(userId, result);
        await repo.SaveRiskFlagsAsync(userId, result.RiskFlags);

        logger.LogInformation(
            "Career stage computed userId={UserId}, stage={Stage}, confidence={Confidence}, risks={RiskCount}, traceId={TraceId}",
            userId, result.Stage, result.ConfidenceLevel, result.RiskFlags.Count, traceId);

        return Results.Ok(new CareerStageResponse
        {
            UserId = userId,
            Stage = result.Stage,
            DisplayName = result.DisplayName,
            ConfidenceLevel = result.ConfidenceLevel,
            RiskFlags = result.RiskFlags.Select(r => new RiskFlagResponse
            {
                Type = r.Type,
                Severity = r.Severity,
                Description = r.Description,
            }).ToList(),
            TriggerSignals = result.TriggerSignals,
            ComputedAt = DateTime.UtcNow,
        });
    }

    private static async Task<IResult> HandleGetCareerStage(
        HttpRequest request,
        IntelligenceRepository repo)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var stage = await repo.GetCurrentCareerStageAsync(userId);
        if (stage is null)
            return EndpointHelpers.NotFound(traceId, ErrorCodes.CareerStageNotFound,
                "No career stage classification found. Compute one first.");

        return Results.Ok(stage);
    }

    private static async Task<IResult> HandleGetCareerStageHistory(
        HttpRequest request,
        IntelligenceRepository repo)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var history = await repo.GetCareerStageHistoryAsync(userId);

        return Results.Ok(new CareerStageHistoryResponse
        {
            History = history,
        });
    }
}

public class CareerStageRequest
{
    public decimal TenureYears { get; set; }
    public int CoLocationCount { get; set; }
    public decimal ProductionVolume { get; set; }
    public string? EntityType { get; set; }
    public int ProviderCount { get; set; }
    public int LocationCount { get; set; }
    public decimal OwnerProductionPct { get; set; }
    public bool HasSuccessionPlan { get; set; }
    public bool HasEntityFormation { get; set; }
    public decimal CeHoursRecent { get; set; }
    public decimal ReputationScore { get; set; }
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

public class CareerStageHistoryResponse
{
    public List<CareerStageResponse> History { get; set; } = new();
}

using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Service.Intelligence.Endpoints;

public static class ScoreEndpoints
{
    public static void MapScoreEndpoints(this WebApplication app)
    {
        app.MapPost("/api/intel/score/compute", HandleComputeScore)
            .WithName("ComputeScore")
            .WithTags("Scoring")
            .Produces<ScoreResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/intel/score", HandleGetScore)
            .WithName("GetCurrentScore")
            .WithTags("Scoring")
            .Produces<ScoreResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/intel/score/history", HandleGetScoreHistory)
            .WithName("GetScoreHistory")
            .WithTags("Scoring")
            .Produces<ScoreHistoryResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleComputeScore(
        HttpRequest request,
        IntelligenceRepository repo,
        ScoringEngine engine,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var contributions = await repo.GetUserDataAsync(userId);
        var result = engine.ComputeScore(contributions);

        await repo.SaveScoreAsync(userId, result.DimensionScores, result.OverallScore, result.ConfidenceLevel);

        logger.LogInformation(
            "Score computed userId={UserId}, overall={Overall}, confidence={Confidence}, traceId={TraceId}",
            userId, result.OverallScore, result.ConfidenceLevel, traceId);

        return Results.Ok(new ScoreResponse
        {
            UserId = userId,
            DimensionScores = result.DimensionScores,
            OverallScore = result.OverallScore,
            ConfidenceLevel = result.ConfidenceLevel,
            ComputedAt = DateTime.UtcNow,
        });
    }

    private static async Task<IResult> HandleGetScore(
        HttpRequest request,
        IntelligenceRepository repo)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var score = await repo.GetCurrentScoreAsync(userId);
        if (score is null)
            return EndpointHelpers.NotFound(traceId, ErrorCodes.ValuationNotFound, "No scoring profile found. Compute a score first.");

        return Results.Ok(new ScoreResponse
        {
            UserId = score.UserId,
            DimensionScores = score.DimensionScores,
            OverallScore = score.OverallScore,
            ConfidenceLevel = score.ConfidenceLevel,
            ComputedAt = score.ComputedAt,
        });
    }

    private static async Task<IResult> HandleGetScoreHistory(
        HttpRequest request,
        IntelligenceRepository repo)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var history = await repo.GetScoreHistoryAsync(userId);

        return Results.Ok(new ScoreHistoryResponse
        {
            History = history.Select(s => new ScoreResponse
            {
                UserId = s.UserId,
                DimensionScores = s.DimensionScores,
                OverallScore = s.OverallScore,
                ConfidenceLevel = s.ConfidenceLevel,
                ComputedAt = s.ComputedAt,
            }).ToList(),
        });
    }
}

public class ScoreResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, decimal> DimensionScores { get; set; } = new();
    public decimal OverallScore { get; set; }
    public string ConfidenceLevel { get; set; } = "low";
    public DateTime ComputedAt { get; set; }
}

public class ScoreHistoryResponse
{
    public List<ScoreResponse> History { get; set; } = new();
}

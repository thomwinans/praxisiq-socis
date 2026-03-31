using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Endpoints;

public static class ContributionEndpoints
{
    public static void MapContributionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/intel/contribute", HandleContribute)
            .WithName("SubmitDataContribution")
            .WithTags("DataContribution")
            .Accepts<SubmitDataRequest>("application/json")
            .Produces<MessageResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/intel/contributions", HandleListContributions)
            .WithName("ListContributions")
            .WithTags("DataContribution")
            .Produces<ContributionListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleContribute(
        [FromBody] SubmitDataRequest body,
        HttpRequest request,
        IntelligenceRepository repo,
        ScoringEngine engine,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Category))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "Category is required.");

        if (body.DataPoints.Count == 0)
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "At least one data point is required.");

        if (!engine.IsValidCategory(body.Category))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, $"Invalid category: {body.Category}");

        var dimension = engine.GetDimensionForCategory(body.Category) ?? body.Category;
        var confidenceContribution = engine.CalculateConfidenceContribution(body.Category);

        var data = new PracticeData
        {
            UserId = userId,
            Dimension = dimension,
            Category = body.Category,
            DataPoints = body.DataPoints,
            ConfidenceContribution = confidenceContribution,
            SubmittedAt = DateTime.UtcNow,
            Source = "self-reported",
        };

        await repo.SubmitDataAsync(data);

        // Recalculate total confidence
        var allData = await repo.GetUserDataAsync(userId);
        var totalConfidence = engine.CalculateTotalConfidence(allData);

        logger.LogInformation(
            "Data contributed userId={UserId}, category={Category}, dimension={Dimension}, confidence={Confidence}, traceId={TraceId}",
            userId, body.Category, dimension, totalConfidence, traceId);

        return Results.Ok(new MessageResponse
        {
            Message = $"Data contributed. Confidence score: {totalConfidence:F1}%",
        });
    }

    private static async Task<IResult> HandleListContributions(
        HttpRequest request,
        IntelligenceRepository repo,
        ScoringEngine engine)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var contributions = await repo.GetUserDataAsync(userId);
        var totalConfidence = engine.CalculateTotalConfidence(contributions);

        var categories = contributions
            .GroupBy(c => c.Category)
            .Select(g => new ContributedCategory
            {
                Category = g.Key,
                Dimension = g.First().Dimension,
                DataPointCount = g.Sum(c => c.DataPoints.Count),
                LatestSubmission = g.Max(c => c.SubmittedAt),
            })
            .ToList();

        return Results.Ok(new ContributionListResponse
        {
            Categories = categories,
            TotalConfidence = totalConfidence,
        });
    }
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

using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Endpoints;

public static class BenchmarkEndpoints
{
    public static void MapBenchmarkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/intel/benchmarks", HandleGetBenchmarks)
            .WithName("GetBenchmarks")
            .WithTags("Benchmarks")
            .Produces<BenchmarkResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetBenchmarks(
        HttpRequest request,
        IntelligenceRepository repo,
        [FromQuery] string specialty,
        [FromQuery] string geo,
        [FromQuery] string size)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(specialty))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "specialty parameter is required.");
        if (string.IsNullOrWhiteSpace(geo))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "geo parameter is required.");
        if (string.IsNullOrWhiteSpace(size))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "size parameter is required.");

        var benchmarks = await repo.GetBenchmarksAsync(specialty, geo, size);

        // Get user's own data for percentile calculation
        var userData = await repo.GetUserDataAsync(userId);
        var userDataMap = userData
            .SelectMany(d => d.DataPoints)
            .GroupBy(dp => dp.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        var metrics = benchmarks.Select(b =>
        {
            var metric = new BenchmarkMetric
            {
                Name = b.MetricName,
                P25 = b.P25,
                P50 = b.P50,
                P75 = b.P75,
            };

            // Calculate user's percentile if they have data for this metric
            if (userDataMap.TryGetValue(b.MetricName, out var userValueStr) &&
                decimal.TryParse(userValueStr, out var userValue))
            {
                metric.UserValue = userValue;
                metric.UserPercentile = CalculatePercentile(userValue, b.P25, b.P50, b.P75);
            }

            return metric;
        }).ToList();

        var cohortSize = benchmarks.Count > 0 ? benchmarks.First().SampleSize : 0;

        return Results.Ok(new BenchmarkResponse
        {
            Metrics = metrics,
            CohortSize = cohortSize,
        });
    }

    private static decimal CalculatePercentile(decimal value, decimal p25, decimal p50, decimal p75)
    {
        if (p75 == p25) return 50m; // Avoid divide by zero

        if (value <= p25) return Math.Max(0, 25m * value / Math.Max(1, p25));
        if (value <= p50) return 25m + 25m * (value - p25) / Math.Max(1, p50 - p25);
        if (value <= p75) return 50m + 25m * (value - p50) / Math.Max(1, p75 - p50);
        return Math.Min(99m, 75m + 25m * (value - p75) / Math.Max(1, p75 - p50));
    }
}

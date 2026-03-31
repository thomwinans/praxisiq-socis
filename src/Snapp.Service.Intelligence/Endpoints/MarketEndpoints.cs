using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Service.Intelligence.Endpoints;

public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this WebApplication app)
    {
        app.MapGet("/api/intel/market/{geoId}", HandleGetMarketProfile)
            .WithName("GetMarketProfile")
            .WithTags("Market Intelligence")
            .Produces<MarketProfileResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/intel/market/compare", HandleCompareMarkets)
            .WithName("CompareMarkets")
            .WithTags("Market Intelligence")
            .Produces<MarketCompareResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetMarketProfile(
        HttpRequest request,
        IntelligenceRepository repo,
        string geoId)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(geoId))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "geoId is required.");

        var profile = await repo.GetMarketProfileAsync(geoId);
        if (profile is null)
            return EndpointHelpers.NotFound(traceId, "MARKET_NOT_FOUND", $"No market data found for geography '{geoId}'.");

        return Results.Ok(profile);
    }

    private static async Task<IResult> HandleCompareMarkets(
        HttpRequest request,
        IntelligenceRepository repo,
        [FromQuery] string geos)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(geos))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "geos query parameter is required (comma-separated, e.g. ?geos=geo1,geo2).");

        var geoIds = geos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (geoIds.Length < 2)
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "At least two geography IDs are required for comparison.");

        var profiles = new List<MarketProfileResponse>();
        foreach (var geoId in geoIds)
        {
            var profile = await repo.GetMarketProfileAsync(geoId);
            if (profile is not null)
                profiles.Add(profile);
        }

        if (profiles.Count < 2)
            return EndpointHelpers.NotFound(traceId, "MARKET_NOT_FOUND", "Could not find market data for at least two of the requested geographies.");

        return Results.Ok(new MarketCompareResponse { Markets = profiles });
    }
}

// ── Response DTOs (service-internal) ────────────────────────────

public class MarketProfileResponse
{
    public string GeoId { get; set; } = string.Empty;
    public string GeoName { get; set; } = string.Empty;
    public decimal PractitionerDensity { get; set; }
    public int CompetitorCount { get; set; }
    public decimal ConsolidationPressure { get; set; }
    public List<DemographicTrend> DemographicTrends { get; set; } = new();
    public List<WorkforceIndicator> WorkforceIndicators { get; set; } = new();
    public DateTime ComputedAt { get; set; }
}

public class DemographicTrend
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // up, down, flat
}

public class WorkforceIndicator
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class MarketCompareResponse
{
    public List<MarketProfileResponse> Markets { get; set; } = new();
}

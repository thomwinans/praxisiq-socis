using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Intelligence.Config;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Endpoints;

public static class CompensationEndpoints
{
    public static void MapCompensationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/intel/compensation/contribute", HandleContributeCompensation)
            .WithName("ContributeCompensation")
            .WithTags("Compensation")
            .Accepts<CompensationContribution>("application/json")
            .Produces<MessageResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/intel/compensation/benchmarks", HandleGetCompensationBenchmarks)
            .WithName("GetCompensationBenchmarks")
            .WithTags("Compensation")
            .Produces<CompensationBenchmarkResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleContributeCompensation(
        [FromBody] CompensationContribution body,
        HttpRequest request,
        IntelligenceRepository repo,
        VerticalConfig config,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Role))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "Role is required.");
        if (string.IsNullOrWhiteSpace(body.CompensationType))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "CompensationType is required.");
        if (string.IsNullOrWhiteSpace(body.AmountBand))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "AmountBand is required.");

        var validRole = config.CompensationRoles.FirstOrDefault(
            r => string.Equals(r.Role, body.Role, StringComparison.OrdinalIgnoreCase));
        if (validRole is null)
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, $"Invalid role: {body.Role}");

        await repo.SaveCompensationContributionAsync(userId, body);

        logger.LogInformation(
            "Compensation contributed userId={UserId}, role={Role}, type={Type}, band={Band}, traceId={TraceId}",
            userId, body.Role, body.CompensationType, body.AmountBand, traceId);

        return Results.Ok(new MessageResponse
        {
            Message = $"Compensation data contributed for {validRole.DisplayName}.",
        });
    }

    private static async Task<IResult> HandleGetCompensationBenchmarks(
        HttpRequest request,
        IntelligenceRepository repo,
        VerticalConfig config,
        [FromQuery] string? market,
        [FromQuery] string? size)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var benchmarks = new List<CompensationRoleBenchmark>();

        foreach (var roleConfig in config.CompensationRoles)
        {
            var contributions = await repo.GetCompensationContributionsForRoleAsync(
                roleConfig.Role, market, size);

            if (contributions.Count < 5)
            {
                benchmarks.Add(new CompensationRoleBenchmark
                {
                    Role = roleConfig.Role,
                    DisplayName = roleConfig.DisplayName,
                    ContributorCount = contributions.Count,
                    MeetsAnonymityThreshold = false,
                });
                continue;
            }

            var bandValues = contributions
                .Select(c => EstimateBandMidpoint(c.AmountBand))
                .Where(v => v > 0)
                .OrderBy(v => v)
                .ToList();

            if (bandValues.Count < 5)
            {
                benchmarks.Add(new CompensationRoleBenchmark
                {
                    Role = roleConfig.Role,
                    DisplayName = roleConfig.DisplayName,
                    ContributorCount = contributions.Count,
                    MeetsAnonymityThreshold = false,
                });
                continue;
            }

            var p25 = Percentile(bandValues, 25);
            var p50 = Percentile(bandValues, 50);
            var p75 = Percentile(bandValues, 75);

            // Determine the compensation type majority
            var dominantType = contributions
                .GroupBy(c => c.CompensationType)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var unit = dominantType switch
            {
                "salary" => "/yr",
                "dailyRate" => "/day",
                _ => "/hr",
            };

            benchmarks.Add(new CompensationRoleBenchmark
            {
                Role = roleConfig.Role,
                DisplayName = roleConfig.DisplayName,
                ContributorCount = contributions.Count,
                MeetsAnonymityThreshold = true,
                P25 = p25,
                P50 = p50,
                P75 = p75,
                CompensationUnit = unit,
                Summary = $"Practices in your market pay {roleConfig.DisplayName} between ${p25:N0} and ${p75:N0}{unit}",
            });

            // Store as COHORT# METRIC#COMP# item
            await repo.SaveBenchmarkAsync(new Benchmark
            {
                MetricName = $"COMP#{roleConfig.Role}",
                P25 = p25,
                P50 = p50,
                P75 = p75,
                SampleSize = contributions.Count,
                ComputedAt = DateTime.UtcNow,
                Geography = market ?? "National",
                Specialty = "dental",
                SizeBand = size ?? "all",
                Vertical = "dental",
                GeographicLevel = "national",
            });
        }

        return Results.Ok(new CompensationBenchmarkResponse
        {
            Roles = benchmarks,
            AnonymityThreshold = 5,
        });
    }

    private static decimal EstimateBandMidpoint(string band)
    {
        // Parse bands like "$30-35/hr", "$50k-60k/yr", "$120k-140k/yr"
        var cleaned = band.Replace("$", "").Replace(",", "").Replace("+", "");

        // Remove unit suffix
        foreach (var suffix in new[] { "/hr", "/yr", "/day" })
            cleaned = cleaned.Replace(suffix, "");

        var parts = cleaned.Split('-');
        if (parts.Length < 1) return 0;

        if (!TryParseWithK(parts[0].Trim(), out var low)) return 0;
        if (parts.Length == 1) return low;
        if (!TryParseWithK(parts[1].Trim(), out var high)) return low;

        return (low + high) / 2m;
    }

    private static bool TryParseWithK(string value, out decimal result)
    {
        if (value.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            if (decimal.TryParse(value[..^1], out var kVal))
            {
                result = kVal * 1000;
                return true;
            }
            result = 0;
            return false;
        }
        return decimal.TryParse(value, out result);
    }

    private static decimal Percentile(List<decimal> sorted, int percentile)
    {
        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper || upper >= sorted.Count) return sorted[lower];
        var weight = (decimal)(index - lower);
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

public class CompensationContribution
{
    public string Role { get; set; } = string.Empty;
    public string CompensationType { get; set; } = string.Empty;
    public string AmountBand { get; set; } = string.Empty;
    public bool BenefitsIncluded { get; set; }
    public bool DentalBenefitsIncluded { get; set; }
    public bool RetirementPlanIncluded { get; set; }
    public bool PaidTimeOff { get; set; }
}

public class CompensationBenchmarkResponse
{
    public List<CompensationRoleBenchmark> Roles { get; set; } = new();
    public int AnonymityThreshold { get; set; } = 5;
}

public class CompensationRoleBenchmark
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ContributorCount { get; set; }
    public bool MeetsAnonymityThreshold { get; set; }
    public decimal P25 { get; set; }
    public decimal P50 { get; set; }
    public decimal P75 { get; set; }
    public string CompensationUnit { get; set; } = "/hr";
    public string? Summary { get; set; }
}

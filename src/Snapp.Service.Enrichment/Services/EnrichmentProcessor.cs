using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class EnrichmentProcessor
{
    private readonly IProviderSource _providerSource;
    private readonly IMarketSource _marketSource;
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<EnrichmentProcessor> _logger;

    // Dental NPPES taxonomy codes
    private static readonly Dictionary<string, List<string>> VerticalTaxonomyCodes = new()
    {
        ["dental"] =
        [
            "1223G0001X", // General Practice Dentistry
            "1223P0221X", // Pediatric Dentistry
            "1223S0112X", // Oral Surgery
            "1223X0400X", // Orthodontics
            "1223E0200X", // Endodontics
            "1223P0106X", // Periodontics
            "1223D0001X", // Dental Public Health
            "1223P0700X", // Prosthodontics
            "1223X0008X", // Oral & Maxillofacial Pathology
        ],
    };

    public EnrichmentProcessor(
        IProviderSource providerSource,
        IMarketSource marketSource,
        EnrichmentRepository repo,
        ILogger<EnrichmentProcessor> logger)
    {
        _providerSource = providerSource;
        _marketSource = marketSource;
        _repo = repo;
        _logger = logger;
    }

    public async Task RunAsync(string vertical)
    {
        await _repo.EnsureTableAsync();

        // Step 1: Provider registry enrichment
        await EnrichProvidersAsync(vertical);

        // Step 2: Geographic & economic market data
        await EnrichMarketsAsync();
    }

    private async Task EnrichProvidersAsync(string vertical)
    {
        var taxonomyCodes = VerticalTaxonomyCodes.GetValueOrDefault(vertical, []);
        if (taxonomyCodes.Count == 0)
        {
            _logger.LogWarning("No taxonomy codes configured for vertical {Vertical}", vertical);
            return;
        }

        _logger.LogInformation("Fetching providers for vertical {Vertical} with {Count} taxonomy codes",
            vertical, taxonomyCodes.Count);

        var providers = await _providerSource.GetProvidersAsync(taxonomyCodes);
        _logger.LogInformation("Processing {Count} provider records", providers.Count);

        var batch = new List<(ProviderRecord Provider, decimal Confidence)>();

        foreach (var provider in providers)
        {
            var confidence = ComputeProviderConfidence(provider);
            batch.Add((provider, confidence));
        }

        if (batch.Count > 0)
        {
            await _repo.SaveProviderSignalsBatchAsync(batch);
            _logger.LogInformation("Saved {Count} provider SIGNAL# items to {Table}",
                batch.Count, "snapp-intel");
        }
    }

    private async Task EnrichMarketsAsync()
    {
        var markets = await _marketSource.GetMarketDataAsync();
        _logger.LogInformation("Processing {Count} market records", markets.Count);

        foreach (var market in markets)
        {
            await _repo.SaveMarketProfileAsync(market);
        }

        _logger.LogInformation("Saved {Count} MKT# profiles to {Table}", markets.Count, "snapp-intel");
    }

    private static decimal ComputeProviderConfidence(ProviderRecord provider)
    {
        var score = 30m; // Base: we have NPI

        if (!string.IsNullOrEmpty(provider.FirstName) && !string.IsNullOrEmpty(provider.LastName))
            score += 10m;
        if (!string.IsNullOrEmpty(provider.Specialty))
            score += 10m;
        if (!string.IsNullOrEmpty(provider.PracticeAddress))
            score += 15m;
        if (!string.IsNullOrEmpty(provider.EnumerationDate))
            score += 10m;
        if (provider.CoLocatedProviderCount > 0)
            score += 10m;
        if (!string.IsNullOrEmpty(provider.Email))
            score += 10m;
        if (!string.IsNullOrEmpty(provider.CountyFips))
            score += 5m;

        return Math.Min(score, 100m);
    }
}

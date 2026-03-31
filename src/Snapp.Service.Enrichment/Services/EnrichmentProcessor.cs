using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class EnrichmentProcessor
{
    private readonly IProviderSource _providerSource;
    private readonly IMarketSource _marketSource;
    private readonly EnrichmentRepository _repo;
    private readonly BenchmarkDataLoader _benchmarkLoader;
    private readonly RegulatoryDataLoader _regulatoryLoader;
    private readonly BusinessListingLoader _businessListingLoader;
    private readonly StateLicensingLoader _licensingLoader;
    private readonly JobPostingLoader _jobPostingLoader;
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
        BenchmarkDataLoader benchmarkLoader,
        RegulatoryDataLoader regulatoryLoader,
        BusinessListingLoader businessListingLoader,
        StateLicensingLoader licensingLoader,
        JobPostingLoader jobPostingLoader,
        ILogger<EnrichmentProcessor> logger)
    {
        _providerSource = providerSource;
        _marketSource = marketSource;
        _repo = repo;
        _benchmarkLoader = benchmarkLoader;
        _regulatoryLoader = regulatoryLoader;
        _businessListingLoader = businessListingLoader;
        _licensingLoader = licensingLoader;
        _jobPostingLoader = jobPostingLoader;
        _logger = logger;
    }

    public async Task RunAsync(string vertical)
    {
        await _repo.EnsureTableAsync();

        // Step 1: Provider registry enrichment
        await EnrichProvidersAsync(vertical);

        // Step 2: Geographic & economic market data
        await EnrichMarketsAsync();

        // Step 3: Association & industry benchmark data (M7.3)
        await LoadBenchmarksAsync();

        // Step 4: Regulatory & claims data (M7.2)
        await LoadRegulatoryDataAsync();

        // Step 5: Business listing integration (M7.4)
        await LoadBusinessListingsAsync(vertical);

        // Step 6: State licensing data (M7.6)
        await LoadStateLicensingAsync(vertical);

        // Step 7: Job posting intelligence (M7.7)
        await LoadJobPostingsAsync();
    }

    private async Task LoadBenchmarksAsync()
    {
        var count = await _benchmarkLoader.LoadBenchmarksAsync();
        _logger.LogInformation("Benchmark enrichment complete: {Count} metrics loaded", count);
    }

    private async Task LoadRegulatoryDataAsync()
    {
        var count = await _regulatoryLoader.LoadRegulatoryDataAsync();
        _logger.LogInformation("Regulatory enrichment complete: {Count} signals loaded", count);
    }

    private async Task LoadBusinessListingsAsync(string vertical)
    {
        var matches = await _businessListingLoader.LoadAndMatchAsync(vertical);
        var reputationCount = matches.Count(m => m.StrongOnlineReputation);
        _logger.LogInformation(
            "Business listing enrichment complete: {MatchCount} matched, {ReputationCount} with strong online reputation",
            matches.Count, reputationCount);
    }

    private async Task LoadStateLicensingAsync(string vertical)
    {
        var matches = await _licensingLoader.LoadAndMatchAsync(vertical);
        var activeCount = matches.Count(m => m.License.Status == "active");
        _logger.LogInformation(
            "State licensing enrichment complete: {MatchCount} matched, {ActiveCount} active licenses",
            matches.Count, activeCount);
    }

    private async Task LoadJobPostingsAsync()
    {
        var analyses = await _jobPostingLoader.LoadAndAnalyzeAsync();
        var chronicCount = analyses.Count(a => a.ChronicTurnoverSignal);
        _logger.LogInformation(
            "Job posting enrichment complete: {PracticeCount} practices analyzed, {ChronicCount} with chronic turnover",
            analyses.Count, chronicCount);
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

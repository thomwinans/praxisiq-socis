using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public class FixtureProviderSource : IProviderSource
{
    private readonly ILogger<FixtureProviderSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FixtureProviderSource(ILogger<FixtureProviderSource> logger) => _logger = logger;

    public async Task<List<ProviderRecord>> GetProvidersAsync(List<string> taxonomyCodes)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "providers.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "providers.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Provider fixture file not found at {Path}", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path);
        var providers = JsonSerializer.Deserialize<List<ProviderRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loaded {Count} providers from fixture", providers.Count);

        if (taxonomyCodes.Count > 0)
        {
            providers = providers
                .Where(p => taxonomyCodes.Contains(p.TaxonomyCode))
                .ToList();
            _logger.LogInformation("Filtered to {Count} providers matching taxonomy codes", providers.Count);
        }

        return providers;
    }
}

public class FixtureMarketSource : IMarketSource
{
    private readonly ILogger<FixtureMarketSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FixtureMarketSource(ILogger<FixtureMarketSource> logger) => _logger = logger;

    public async Task<List<MarketRecord>> GetMarketDataAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "markets.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "markets.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Market fixture file not found at {Path}", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path);
        var markets = JsonSerializer.Deserialize<List<MarketRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loaded {Count} market records from fixture", markets.Count);
        return markets;
    }
}

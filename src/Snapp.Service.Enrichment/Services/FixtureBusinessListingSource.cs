using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public class FixtureBusinessListingSource : IBusinessListingProvider
{
    private readonly ILogger<FixtureBusinessListingSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FixtureBusinessListingSource(ILogger<FixtureBusinessListingSource> logger) => _logger = logger;

    public async Task<List<BusinessListingRecord>> GetListingsAsync(string vertical, string? state = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "business-listings.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "business-listings.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Business listing fixture not found at {Path}", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path);
        var listings = JsonSerializer.Deserialize<List<BusinessListingRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loaded {Count} business listings from fixture", listings.Count);

        if (!string.IsNullOrEmpty(state))
        {
            listings = listings
                .Where(l => l.State.Equals(state, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _logger.LogInformation("Filtered to {Count} listings in state {State}", listings.Count, state);
        }

        return listings;
    }
}

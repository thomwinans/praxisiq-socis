using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public class FixtureStateLicensingSource : IStateLicensingSource
{
    private readonly ILogger<FixtureStateLicensingSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FixtureStateLicensingSource(ILogger<FixtureStateLicensingSource> logger) => _logger = logger;

    public async Task<List<StateLicensingRecord>> GetLicensesAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "licensing", "state-dental-licenses.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "licensing", "state-dental-licenses.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("State licensing fixture file not found at {Path}", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path);
        var licenses = JsonSerializer.Deserialize<List<StateLicensingRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loaded {Count} state licensing records from fixture", licenses.Count);
        return licenses;
    }
}

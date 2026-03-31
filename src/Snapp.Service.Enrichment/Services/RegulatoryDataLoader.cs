using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class RegulatoryDataLoader
{
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<RegulatoryDataLoader> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RegulatoryDataLoader(EnrichmentRepository repo, ILogger<RegulatoryDataLoader> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<int> LoadRegulatoryDataAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "regulatory", "cms-provider-data.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "regulatory", "cms-provider-data.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Regulatory fixture not found: {Path}", path);
            return 0;
        }

        var json = await File.ReadAllTextAsync(path);
        var records = JsonSerializer.Deserialize<List<RegulatoryRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loading {Count} regulatory signal records", records.Count);

        await _repo.SaveRegulatorySignalsBatchAsync(records);

        _logger.LogInformation("Loaded {Count} regulatory SIGNAL# items to snapp-intel", records.Count);
        return records.Count;
    }
}

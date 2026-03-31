using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class BenchmarkDataLoader
{
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<BenchmarkDataLoader> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] BenchmarkFiles =
    [
        "benchmarks/revenue-quartiles.json",
        "benchmarks/compensation-ranges.json",
        "benchmarks/overhead-ratios.json",
        "benchmarks/production-metrics.json",
    ];

    public BenchmarkDataLoader(EnrichmentRepository repo, ILogger<BenchmarkDataLoader> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<int> LoadBenchmarksAsync()
    {
        var totalLoaded = 0;

        foreach (var file in BenchmarkFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", file);

            if (!File.Exists(path))
            {
                _logger.LogWarning("Benchmark fixture not found: {Path}", path);
                continue;
            }

            var json = await File.ReadAllTextAsync(path);
            var records = JsonSerializer.Deserialize<List<BenchmarkRecord>>(json, JsonOptions) ?? [];

            _logger.LogInformation("Loading {Count} benchmark records from {File}", records.Count, file);

            await _repo.SaveBenchmarksBatchAsync(records);
            totalLoaded += records.Count;
        }

        _logger.LogInformation("Loaded {Total} total benchmark items (BENCH#/COHORT#) to snapp-intel", totalLoaded);
        return totalLoaded;
    }
}

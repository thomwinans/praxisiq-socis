using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public class FixtureJobPostingSource : IJobPostingSource
{
    private readonly ILogger<FixtureJobPostingSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FixtureJobPostingSource(ILogger<FixtureJobPostingSource> logger) => _logger = logger;

    public async Task<List<JobPostingRecord>> GetJobPostingsAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "job-postings", "practice-job-postings.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "job-postings", "practice-job-postings.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Job posting fixture file not found at {Path}", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path);
        var postings = JsonSerializer.Deserialize<List<JobPostingRecord>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Loaded {Count} job posting records from fixture", postings.Count);
        return postings;
    }
}

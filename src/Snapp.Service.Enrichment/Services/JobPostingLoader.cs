using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class JobPostingLoader
{
    private readonly IJobPostingSource _source;
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<JobPostingLoader> _logger;

    private const int ChronicTurnoverThreshold = 3;

    public JobPostingLoader(
        IJobPostingSource source,
        EnrichmentRepository repo,
        ILogger<JobPostingLoader> logger)
    {
        _source = source;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<JobPostingAnalysis>> LoadAndAnalyzeAsync()
    {
        var postings = await _source.GetJobPostingsAsync();
        if (postings.Count == 0)
        {
            _logger.LogWarning("No job posting records found");
            return [];
        }

        // Group by practice (name + city + state)
        var grouped = postings
            .GroupBy(p => $"{p.PracticeName}|{p.PracticeCity}|{p.PracticeState}")
            .ToList();

        var analyses = new List<JobPostingAnalysis>();

        foreach (var group in grouped)
        {
            var practicePostings = group.ToList();
            var analysis = AnalyzePractice(practicePostings);
            analyses.Add(analysis);
        }

        _logger.LogInformation(
            "Analyzed {PracticeCount} practices from {PostingCount} job postings",
            analyses.Count, postings.Count);

        var chronicCount = analyses.Count(a => a.ChronicTurnoverSignal);
        _logger.LogInformation(
            "{ChronicCount}/{Total} practices show chronic turnover signals",
            chronicCount, analyses.Count);

        if (analyses.Count > 0)
            await _repo.SaveJobPostingSignalsBatchAsync(analyses);

        return analyses;
    }

    private static JobPostingAnalysis AnalyzePractice(List<JobPostingRecord> postings)
    {
        var first = postings[0];

        // Role repetition analysis
        var roleGroups = postings
            .GroupBy(p => p.Role, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RoleRepetition
            {
                Role = g.Key,
                Count = g.Count(),
                IsChronicTurnover = g.Count() >= ChronicTurnoverThreshold,
            })
            .ToList();

        var hasChronicTurnover = roleGroups.Any(r => r.IsChronicTurnover);
        var urgentCount = postings.Count(p => p.HasUrgencyLanguage);

        // Posting frequency: postings per 12-month window
        var dates = postings
            .Select(p => DateTime.TryParse(p.PostingDate, out var d) ? d : DateTime.MinValue)
            .Where(d => d != DateTime.MinValue)
            .OrderBy(d => d)
            .ToList();

        decimal postingFrequency = 0;
        if (dates.Count >= 2)
        {
            var spanMonths = (decimal)(dates.Last() - dates.First()).TotalDays / 30.44m;
            postingFrequency = spanMonths > 0 ? postings.Count / spanMonths * 12m : postings.Count;
        }
        else
        {
            postingFrequency = postings.Count;
        }

        var workforcePressure = ComputeWorkforcePressureScore(
            postings.Count, urgentCount, hasChronicTurnover, postingFrequency);

        return new JobPostingAnalysis
        {
            PracticeName = first.PracticeName,
            PracticeCity = first.PracticeCity,
            PracticeState = first.PracticeState,
            TotalPostings = postings.Count,
            UniqueRoles = roleGroups.Count,
            UrgentPostings = urgentCount,
            PostingFrequency = Math.Round(postingFrequency, 2),
            ChronicTurnoverSignal = hasChronicTurnover,
            WorkforcePressureScore = workforcePressure,
            RoleRepetitions = roleGroups,
            Postings = postings,
        };
    }

    /// <summary>
    /// Computes a 0-100 workforce pressure score based on posting signals.
    /// Higher = more workforce pressure.
    /// </summary>
    private static decimal ComputeWorkforcePressureScore(
        int totalPostings, int urgentPostings, bool chronicTurnover, decimal postingFrequency)
    {
        var score = 0m;

        // Volume factor: more postings = higher pressure (0-30 pts)
        score += Math.Min(30m, totalPostings * 7.5m);

        // Urgency factor: urgent language signals desperation (0-25 pts)
        if (totalPostings > 0)
            score += (decimal)urgentPostings / totalPostings * 25m;

        // Chronic turnover: same role posted 3+ times is a strong signal (0-25 pts)
        if (chronicTurnover)
            score += 25m;

        // Posting frequency: high frequency = churn (0-20 pts)
        score += Math.Min(20m, postingFrequency * 2.5m);

        return Math.Min(100m, Math.Round(score, 2));
    }
}

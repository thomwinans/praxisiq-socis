using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class StateLicensingLoader
{
    private readonly IStateLicensingSource _source;
    private readonly IProviderSource _providerSource;
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<StateLicensingLoader> _logger;

    public StateLicensingLoader(
        IStateLicensingSource source,
        IProviderSource providerSource,
        EnrichmentRepository repo,
        ILogger<StateLicensingLoader> logger)
    {
        _source = source;
        _providerSource = providerSource;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<LicensingMatchResult>> LoadAndMatchAsync(string vertical)
    {
        var licenses = await _source.GetLicensesAsync();
        if (licenses.Count == 0)
        {
            _logger.LogWarning("No state licensing records found");
            return [];
        }

        // Load all providers (empty taxonomy list = all)
        var providers = await _providerSource.GetProvidersAsync([]);

        var matches = new List<LicensingMatchResult>();

        foreach (var license in licenses)
        {
            var (provider, method, confidence) = FindBestProviderMatch(license, providers);
            if (provider is null) continue;

            var tenureYears = ComputeTenureYears(license.IssueDate);

            matches.Add(new LicensingMatchResult
            {
                License = license,
                Provider = provider,
                MatchMethod = method,
                MatchConfidence = confidence,
                TenureYearsFromLicense = tenureYears,
            });
        }

        _logger.LogInformation(
            "Matched {MatchCount}/{Total} licensing records to providers",
            matches.Count, licenses.Count);

        if (matches.Count > 0)
            await _repo.SaveStateLicensingSignalsBatchAsync(matches);

        return matches;
    }

    private static (ProviderRecord? Provider, string Method, decimal Confidence) FindBestProviderMatch(
        StateLicensingRecord license, List<ProviderRecord> providers)
    {
        // Exact name + city match
        var exactMatch = providers.FirstOrDefault(p =>
            string.Equals(p.FirstName, license.ProviderFirstName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.LastName, license.ProviderLastName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.City, license.City, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
            return (exactMatch, "name-city-exact", 0.95m);

        // Name exact + same state (city mismatch — may have moved offices)
        var stateMatch = providers.FirstOrDefault(p =>
            string.Equals(p.FirstName, license.ProviderFirstName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.LastName, license.ProviderLastName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.State, license.State, StringComparison.OrdinalIgnoreCase));

        if (stateMatch is not null)
            return (stateMatch, "name-state-exact", 0.80m);

        // Fuzzy: last name + city + state (first name may differ — abbreviation, nickname)
        var fuzzyMatch = providers.FirstOrDefault(p =>
            string.Equals(p.LastName, license.ProviderLastName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.City, license.City, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.State, license.State, StringComparison.OrdinalIgnoreCase));

        if (fuzzyMatch is not null)
            return (fuzzyMatch, "lastname-city-fuzzy", 0.65m);

        return (null, "", 0m);
    }

    private static decimal ComputeTenureYears(string issueDate)
    {
        if (!DateTime.TryParse(issueDate, out var issued))
            return 0m;

        var years = (decimal)(DateTime.UtcNow - issued).TotalDays / 365.25m;
        return Math.Max(0, Math.Round(years, 1));
    }
}

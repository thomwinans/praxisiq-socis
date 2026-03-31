using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;

namespace Snapp.Service.Enrichment.Services;

public class BusinessListingLoader
{
    private readonly IBusinessListingProvider _listingProvider;
    private readonly IProviderSource _providerSource;
    private readonly EnrichmentRepository _repo;
    private readonly ILogger<BusinessListingLoader> _logger;

    // Dental NPPES taxonomy codes (same as EnrichmentProcessor)
    private static readonly List<string> DentalTaxonomyCodes =
    [
        "1223G0001X", "1223P0221X", "1223S0112X", "1223X0400X",
        "1223E0200X", "1223P0106X", "1223D0001X", "1223P0700X", "1223X0008X",
    ];

    public BusinessListingLoader(
        IBusinessListingProvider listingProvider,
        IProviderSource providerSource,
        EnrichmentRepository repo,
        ILogger<BusinessListingLoader> logger)
    {
        _listingProvider = listingProvider;
        _providerSource = providerSource;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<ListingMatchResult>> LoadAndMatchAsync(string vertical)
    {
        var listings = await _listingProvider.GetListingsAsync(vertical);
        var providers = await _providerSource.GetProvidersAsync(DentalTaxonomyCodes);

        _logger.LogInformation(
            "Business listing matching: {ListingCount} listings against {ProviderCount} providers",
            listings.Count, providers.Count);

        var matched = new List<ListingMatchResult>();
        var matchedProviderNpis = new HashSet<string>();
        var matchedListingIds = new HashSet<string>();

        // Pass 1: Phone exact match (confidence 1.0)
        var providersByPhone = providers
            .Where(p => !string.IsNullOrEmpty(p.Phone))
            .ToDictionary(p => NormalizePhone(p.Phone!), p => p);

        foreach (var listing in listings)
        {
            if (string.IsNullOrEmpty(listing.Phone)) continue;

            var normalizedPhone = NormalizePhone(listing.Phone);
            if (providersByPhone.TryGetValue(normalizedPhone, out var provider)
                && !matchedProviderNpis.Contains(provider.Npi))
            {
                matched.Add(CreateMatch(listing, provider, "phone-exact", 1.0m));
                matchedProviderNpis.Add(provider.Npi);
                matchedListingIds.Add(listing.PlaceId);
            }
        }

        _logger.LogInformation("Pass 1 (phone exact): {Count} matches", matched.Count);

        // Pass 2: Address exact match (confidence 0.8)
        var pass2Count = 0;
        var providersByAddress = providers
            .Where(p => !matchedProviderNpis.Contains(p.Npi))
            .GroupBy(p => NormalizeAddress(p.PracticeAddress, p.City, p.State))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var listing in listings)
        {
            if (matchedListingIds.Contains(listing.PlaceId)) continue;

            var normalizedAddr = NormalizeAddress(listing.Address, listing.City, listing.State);
            if (providersByAddress.TryGetValue(normalizedAddr, out var provider)
                && !matchedProviderNpis.Contains(provider.Npi))
            {
                matched.Add(CreateMatch(listing, provider, "address-exact", 0.8m));
                matchedProviderNpis.Add(provider.Npi);
                matchedListingIds.Add(listing.PlaceId);
                pass2Count++;
            }
        }

        _logger.LogInformation("Pass 2 (address exact): {Count} matches", pass2Count);

        // Pass 3: Name + city fuzzy match (confidence 0.5–0.7)
        var pass3Count = 0;
        var unmatchedProviders = providers
            .Where(p => !matchedProviderNpis.Contains(p.Npi))
            .ToList();

        foreach (var listing in listings)
        {
            if (matchedListingIds.Contains(listing.PlaceId)) continue;

            foreach (var provider in unmatchedProviders)
            {
                if (matchedProviderNpis.Contains(provider.Npi)) continue;
                if (!listing.City.Equals(provider.City, StringComparison.OrdinalIgnoreCase)) continue;

                var confidence = ComputeNameMatchConfidence(listing.Name, provider);
                if (confidence >= 0.5m)
                {
                    matched.Add(CreateMatch(listing, provider, "name-city-fuzzy", confidence));
                    matchedProviderNpis.Add(provider.Npi);
                    matchedListingIds.Add(listing.PlaceId);
                    pass3Count++;
                    break;
                }
            }
        }

        _logger.LogInformation("Pass 3 (name+city fuzzy): {Count} matches", pass3Count);
        _logger.LogInformation("Total matched: {Count} of {Total} listings",
            matched.Count, listings.Count);

        // Save matched listing signals to DynamoDB
        if (matched.Count > 0)
        {
            await _repo.SaveBusinessListingSignalsBatchAsync(matched);
            _logger.LogInformation("Saved {Count} LISTING# signals to snapp-intel", matched.Count);
        }

        return matched;
    }

    private static ListingMatchResult CreateMatch(
        BusinessListingRecord listing,
        ProviderRecord provider,
        string method,
        decimal confidence)
    {
        return new ListingMatchResult
        {
            Listing = listing,
            Provider = provider,
            MatchMethod = method,
            MatchConfidence = confidence,
            StrongOnlineReputation = listing.Rating >= 4.5m && listing.ReviewCount >= 100,
        };
    }

    private static decimal ComputeNameMatchConfidence(string listingName, ProviderRecord provider)
    {
        var normalizedListing = listingName.ToLowerInvariant();
        var lastName = provider.LastName.ToLowerInvariant();

        // Strong match: last name appears in listing name
        if (normalizedListing.Contains(lastName))
        {
            // Extra confidence if first name also matches
            var firstName = provider.FirstName.ToLowerInvariant();
            if (normalizedListing.Contains(firstName))
                return 0.7m;

            return 0.6m;
        }

        // Check if specialty keyword matches listing category/name
        var specialty = provider.Specialty.ToLowerInvariant();
        if (!string.IsNullOrEmpty(specialty) && normalizedListing.Contains(GetSpecialtyKeyword(specialty)))
            return 0.5m;

        return 0m;
    }

    private static string GetSpecialtyKeyword(string specialty) => specialty switch
    {
        "general practice dentistry" => "dental",
        "pediatric dentistry" => "pediatric",
        "orthodontics" => "orthodon",
        "oral surgery" => "oral surg",
        "endodontics" => "endodont",
        "periodontics" => "periodont",
        "prosthodontics" => "prosthodont",
        _ => specialty,
    };

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static string NormalizeAddress(string address, string city, string state) =>
        $"{address.Trim().ToLowerInvariant()}|{city.Trim().ToLowerInvariant()}|{state.Trim().ToUpperInvariant()}";
}

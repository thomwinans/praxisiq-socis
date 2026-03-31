using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

/// <summary>
/// Stub Google Places API client for future production use.
/// Currently throws NotImplementedException — use FixtureBusinessListingSource for dev.
/// </summary>
public class GooglePlacesClient : IBusinessListingProvider
{
    private readonly ILogger<GooglePlacesClient> _logger;

    public GooglePlacesClient(ILogger<GooglePlacesClient> logger) => _logger = logger;

    public Task<List<BusinessListingRecord>> GetListingsAsync(string vertical, string? state = null)
    {
        // Future: call Google Places API with text search for "{vertical} {state}"
        // GET https://maps.googleapis.com/maps/api/place/textsearch/json
        //   ?query=dentist+in+{state}
        //   &key={API_KEY}
        // Then paginate with next_page_token, map to BusinessListingRecord
        _logger.LogWarning("GooglePlacesClient is a stub — configure Enrichment:ListingSource=fixture for dev");
        throw new NotImplementedException("Google Places API integration not yet implemented. Use fixture source for development.");
    }
}

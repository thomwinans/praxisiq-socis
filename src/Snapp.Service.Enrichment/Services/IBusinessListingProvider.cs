using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public interface IBusinessListingProvider
{
    Task<List<BusinessListingRecord>> GetListingsAsync(string vertical, string? state = null);
}

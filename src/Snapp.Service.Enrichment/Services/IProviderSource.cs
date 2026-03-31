using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public interface IProviderSource
{
    Task<List<ProviderRecord>> GetProvidersAsync(List<string> taxonomyCodes);
}

public interface IMarketSource
{
    Task<List<MarketRecord>> GetMarketDataAsync();
}

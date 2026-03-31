using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public class NppesProviderSource : IProviderSource
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<NppesProviderSource> _logger;
    private const string NppesBaseUrl = "https://npiregistry.cms.hhs.gov/api/";
    private const int PageSize = 200;

    public NppesProviderSource(IHttpClientFactory httpFactory, ILogger<NppesProviderSource> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<List<ProviderRecord>> GetProvidersAsync(List<string> taxonomyCodes)
    {
        var allProviders = new List<ProviderRecord>();

        foreach (var taxonomy in taxonomyCodes)
        {
            var skip = 0;
            var hasMore = true;

            while (hasMore)
            {
                try
                {
                    var batch = await FetchPageAsync(taxonomy, skip);
                    allProviders.AddRange(batch);
                    hasMore = batch.Count == PageSize;
                    skip += PageSize;

                    // Rate limit: NPPES API is public, be respectful
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NPPES API error for taxonomy {Taxonomy} at skip {Skip}", taxonomy, skip);
                    hasMore = false;
                }
            }
        }

        _logger.LogInformation("Fetched {Count} providers from NPPES API", allProviders.Count);
        return allProviders;
    }

    private async Task<List<ProviderRecord>> FetchPageAsync(string taxonomyCode, int skip)
    {
        var client = _httpFactory.CreateClient();
        var url = $"{NppesBaseUrl}?version=2.1&taxonomy_description={Uri.EscapeDataString(taxonomyCode)}&limit={PageSize}&skip={skip}";

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var results = doc.RootElement.GetProperty("results");
        var providers = new List<ProviderRecord>();

        foreach (var result in results.EnumerateArray())
        {
            var record = MapNppesResult(result);
            if (record is not null)
                providers.Add(record);
        }

        return providers;
    }

    private static ProviderRecord? MapNppesResult(JsonElement result)
    {
        var basic = result.GetProperty("basic");
        var number = result.GetProperty("number").GetString();
        if (string.IsNullOrEmpty(number)) return null;

        var record = new ProviderRecord
        {
            Npi = number,
            FirstName = GetString(basic, "first_name"),
            LastName = GetString(basic, "last_name"),
            Credential = GetString(basic, "credential"),
            EnumerationDate = GetString(basic, "enumeration_date"),
            EntityType = result.GetProperty("enumeration_type").GetString() == "NPI-1" ? "individual" : "organization",
        };

        if (record.EntityType == "organization")
            record.OrganizationName = GetString(basic, "organization_name");

        // Primary practice address
        if (result.TryGetProperty("addresses", out var addresses))
        {
            foreach (var addr in addresses.EnumerateArray())
            {
                if (GetString(addr, "address_purpose") == "LOCATION")
                {
                    record.PracticeAddress = $"{GetString(addr, "address_1")} {GetString(addr, "address_2")}".Trim();
                    record.City = GetString(addr, "city");
                    record.State = GetString(addr, "state");
                    record.ZipCode = GetString(addr, "postal_code");
                    break;
                }
            }
        }

        // Primary taxonomy
        if (result.TryGetProperty("taxonomies", out var taxonomies))
        {
            foreach (var tax in taxonomies.EnumerateArray())
            {
                if (tax.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                {
                    record.TaxonomyCode = GetString(tax, "code");
                    record.Specialty = GetString(tax, "desc");
                    break;
                }
            }
        }

        return record;
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var val) ? val.GetString() ?? string.Empty : string.Empty;
}

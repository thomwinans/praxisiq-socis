namespace Snapp.Service.Enrichment.Models;

public class BusinessListingRecord
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Category { get; set; }
}

public class ListingMatchResult
{
    public BusinessListingRecord Listing { get; set; } = null!;
    public ProviderRecord Provider { get; set; } = null!;
    public string MatchMethod { get; set; } = string.Empty;
    public decimal MatchConfidence { get; set; }
    public bool StrongOnlineReputation { get; set; }
}

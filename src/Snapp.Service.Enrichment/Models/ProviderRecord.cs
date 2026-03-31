namespace Snapp.Service.Enrichment.Models;

public class ProviderRecord
{
    public string Npi { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string TaxonomyCode { get; set; } = string.Empty;
    public string PracticeAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string CountyFips { get; set; } = string.Empty;
    public string EnumerationDate { get; set; } = string.Empty;
    public int CoLocatedProviderCount { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? OrganizationName { get; set; }
    public string EntityType { get; set; } = "individual";
}

public class MarketRecord
{
    public string CountyFips { get; set; } = string.Empty;
    public string CountyName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Population { get; set; }
    public decimal MedianHouseholdIncome { get; set; }
    public decimal PopulationGrowthRate { get; set; }
    public decimal MedianAge { get; set; }
    public int DentalProviderCount { get; set; }
    public decimal ProvidersPer100K { get; set; }
    public int DsoLocationCount { get; set; }
    public decimal MedianHomeValue { get; set; }
    public decimal UninsuredRate { get; set; }
}

namespace Snapp.Service.Enrichment.Models;

public class StateLicensingRecord
{
    public string LicenseNumber { get; set; } = string.Empty;
    public string ProviderFirstName { get; set; } = string.Empty;
    public string ProviderLastName { get; set; } = string.Empty;
    public string CredentialType { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string IssueDate { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
}

public class LicensingMatchResult
{
    public StateLicensingRecord License { get; set; } = null!;
    public ProviderRecord Provider { get; set; } = null!;
    public string MatchMethod { get; set; } = string.Empty;
    public decimal MatchConfidence { get; set; }
    public decimal TenureYearsFromLicense { get; set; }
}

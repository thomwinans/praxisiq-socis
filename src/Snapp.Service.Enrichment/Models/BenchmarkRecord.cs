namespace Snapp.Service.Enrichment.Models;

public class BenchmarkRecord
{
    public string Vertical { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string SizeBand { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string GeographicLevel { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal P25 { get; set; }
    public decimal P50 { get; set; }
    public decimal P75 { get; set; }
    public decimal? Mean { get; set; }
    public int SampleSize { get; set; }
}

public class RegulatoryRecord
{
    public string Npi { get; set; } = string.Empty;
    public int TotalPrescriptions { get; set; }
    public int OpioidPrescriptions { get; set; }
    public int AntibioticPrescriptions { get; set; }
    public int TotalBeneficiaries { get; set; }
    public decimal AverageBeneficiaryAge { get; set; }
    public decimal FemaleBeneficiaryPct { get; set; }
    public decimal DualEligiblePct { get; set; }
    public decimal AverageRiskScore { get; set; }
    public decimal TotalMedicarePayments { get; set; }
    public int GraduationYear { get; set; }
    public string MedicalSchool { get; set; } = string.Empty;
    public string Source { get; set; } = "cms-fixture";
}

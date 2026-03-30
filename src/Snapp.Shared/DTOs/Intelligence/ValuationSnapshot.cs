namespace Snapp.Shared.DTOs.Intelligence;

public class ValuationSnapshot
{
    public DateTime Date { get; set; }

    public decimal Base { get; set; }

    public decimal ConfidenceScore { get; set; }
}

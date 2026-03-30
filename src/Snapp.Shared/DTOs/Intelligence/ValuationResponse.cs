namespace Snapp.Shared.DTOs.Intelligence;

public class ValuationResponse
{
    public decimal Downside { get; set; }

    public decimal Base { get; set; }

    public decimal Upside { get; set; }

    public decimal ConfidenceScore { get; set; }

    public List<ValuationDriver> Drivers { get; set; } = new();

    public List<ValuationSnapshot> History { get; set; } = new();
}

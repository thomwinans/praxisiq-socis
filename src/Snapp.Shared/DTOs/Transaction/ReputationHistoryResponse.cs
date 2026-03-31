namespace Snapp.Shared.DTOs.Transaction;

public class ReputationHistoryResponse
{
    public List<ReputationHistoryPoint> Points { get; set; } = new();
}

public class ReputationHistoryPoint
{
    public DateTime Date { get; set; }

    public decimal OverallScore { get; set; }
}

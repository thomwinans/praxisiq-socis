namespace Snapp.Shared.DTOs.Transaction;

public class ReputationResponse
{
    public string UserId { get; set; } = string.Empty;

    public decimal OverallScore { get; set; }

    public decimal ReferralScore { get; set; }

    public decimal ContributionScore { get; set; }

    public decimal AttestationScore { get; set; }

    public DateTime ComputedAt { get; set; }
}

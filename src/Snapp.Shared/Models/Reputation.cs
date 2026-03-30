using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class Reputation
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public decimal OverallScore { get; set; }

    public decimal ReferralScore { get; set; }

    public decimal ContributionScore { get; set; }

    public decimal AttestationScore { get; set; }

    public DateTime ComputedAt { get; set; }
}

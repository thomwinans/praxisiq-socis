using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class NetworkMembership
{
    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTime JoinedAt { get; set; }

    public decimal ContributionScore { get; set; }
}

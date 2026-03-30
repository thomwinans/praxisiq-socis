namespace Snapp.Shared.DTOs.Network;

public class MemberResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; }

    public decimal ContributionScore { get; set; }
}

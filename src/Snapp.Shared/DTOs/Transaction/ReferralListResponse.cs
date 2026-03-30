namespace Snapp.Shared.DTOs.Transaction;

public class ReferralListResponse
{
    public List<ReferralResponse> Referrals { get; set; } = new();

    public string? NextToken { get; set; }
}

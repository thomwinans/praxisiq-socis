namespace Snapp.Shared.DTOs.Network;

public class MemberListResponse
{
    public List<MemberResponse> Members { get; set; } = new();

    public string? NextToken { get; set; }
}

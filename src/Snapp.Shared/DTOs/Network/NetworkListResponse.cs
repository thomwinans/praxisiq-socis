namespace Snapp.Shared.DTOs.Network;

public class NetworkListResponse
{
    public List<NetworkResponse> Networks { get; set; } = new();

    public string? NextToken { get; set; }
}

namespace Snapp.Shared.DTOs.Network;

public class NetworkResponse
{
    public string NetworkId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Charter { get; set; }

    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? UserRole { get; set; }
}

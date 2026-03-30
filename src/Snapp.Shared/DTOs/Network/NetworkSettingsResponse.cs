namespace Snapp.Shared.DTOs.Network;

public class NetworkSettingsResponse
{
    public NetworkResponse Network { get; set; } = new();

    public List<RoleResponse> Roles { get; set; } = new();

    public int PendingApplicationCount { get; set; }
}

public class RoleResponse
{
    public string RoleName { get; set; } = string.Empty;

    public int Permissions { get; set; }

    public string? Description { get; set; }
}

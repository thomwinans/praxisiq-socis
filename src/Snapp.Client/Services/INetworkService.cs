using Snapp.Shared.DTOs.Network;

namespace Snapp.Client.Services;

public interface INetworkService
{
    Task<NetworkListResponse> GetMyNetworksAsync();
    Task<NetworkListResponse> GetAllNetworksAsync();
    Task<NetworkResponse?> GetNetworkAsync(string networkId);
    Task<NetworkResponse?> CreateNetworkAsync(CreateNetworkRequest request);
    Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request);
    Task<MemberListResponse> GetMembersAsync(string networkId);
    Task<bool> ApplyAsync(string networkId, string? applicationText);
    Task<List<ApplicationResponse>> GetApplicationsAsync(string networkId);
    Task<bool> DecideApplicationAsync(string networkId, string userId, string decision, string? reason = null);
    Task<NetworkSettingsResponse?> GetSettingsAsync(string networkId);
    Task<bool> RemoveMemberAsync(string networkId, string userId);
    Task<bool> ChangeMemberRoleAsync(string networkId, string userId, string newRole);
}

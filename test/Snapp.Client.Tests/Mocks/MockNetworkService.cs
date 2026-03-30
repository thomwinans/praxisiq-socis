using Snapp.Client.Services;
using Snapp.Shared.DTOs.Network;

namespace Snapp.Client.Tests.Mocks;

public class MockNetworkService : INetworkService
{
    public NetworkListResponse AllNetworks { get; set; } = new();
    public NetworkListResponse MyNetworks { get; set; } = new();
    public NetworkResponse? Network { get; set; }
    public NetworkResponse? CreatedNetwork { get; set; }
    public MemberListResponse Members { get; set; } = new();
    public List<ApplicationResponse> Applications { get; set; } = new();
    public bool ShouldSucceed { get; set; } = true;
    public bool ShouldThrow { get; set; }

    public CreateNetworkRequest? LastCreateRequest { get; private set; }
    public string? LastApplyNetworkId { get; private set; }
    public string? LastApplyText { get; private set; }
    public string? LastDecisionUserId { get; private set; }
    public string? LastDecision { get; private set; }

    public Task<NetworkListResponse> GetAllNetworksAsync()
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(AllNetworks);
    }

    public Task<NetworkListResponse> GetMyNetworksAsync()
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(MyNetworks);
    }

    public Task<NetworkResponse?> GetNetworkAsync(string networkId)
    {
        return Task.FromResult(Network);
    }

    public Task<NetworkResponse?> CreateNetworkAsync(CreateNetworkRequest request)
    {
        LastCreateRequest = request;
        return Task.FromResult(CreatedNetwork);
    }

    public Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request)
    {
        return Task.FromResult(ShouldSucceed);
    }

    public Task<MemberListResponse> GetMembersAsync(string networkId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(Members);
    }

    public Task<bool> ApplyAsync(string networkId, string? applicationText)
    {
        LastApplyNetworkId = networkId;
        LastApplyText = applicationText;
        return Task.FromResult(ShouldSucceed);
    }

    public Task<List<ApplicationResponse>> GetApplicationsAsync(string networkId)
    {
        return Task.FromResult(Applications);
    }

    public Task<bool> DecideApplicationAsync(string networkId, string userId, string decision, string? reason = null)
    {
        LastDecisionUserId = userId;
        LastDecision = decision;
        return Task.FromResult(ShouldSucceed);
    }

    public Task<bool> RemoveMemberAsync(string networkId, string userId)
    {
        return Task.FromResult(ShouldSucceed);
    }

    public Task<bool> ChangeMemberRoleAsync(string networkId, string userId, string newRole)
    {
        return Task.FromResult(ShouldSucceed);
    }
}

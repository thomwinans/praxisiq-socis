using Snapp.Shared.DTOs.Network;

namespace Snapp.Client.Services;

public interface INetworkService
{
    Task<NetworkListResponse> GetMyNetworksAsync();
}

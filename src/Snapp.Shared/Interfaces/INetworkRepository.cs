using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface INetworkRepository
{
    Task<Network?> GetByIdAsync(string networkId);

    Task CreateAsync(Network network);

    Task UpdateAsync(Network network);

    Task<List<Network>> ListAsync(string? nextToken);

    Task<NetworkMembership?> GetMembershipAsync(string networkId, string userId);

    Task AddMemberAsync(NetworkMembership membership);

    Task UpdateMemberAsync(NetworkMembership membership);

    Task<List<NetworkMembership>> ListMembersAsync(string networkId, string? nextToken);

    Task<List<Network>> ListUserNetworksAsync(string userId, string? nextToken);
}

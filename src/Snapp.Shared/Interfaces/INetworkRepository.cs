using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-networks DynamoDB table.
/// Handles network metadata, membership, and application records.
/// </summary>
public interface INetworkRepository
{
    /// <summary>Retrieves network metadata by ID. Returns null if not found.</summary>
    Task<Network?> GetByIdAsync(string networkId);

    /// <summary>Creates a new network metadata item.</summary>
    Task CreateAsync(Network network);

    /// <summary>Updates an existing network metadata item.</summary>
    Task UpdateAsync(Network network);

    /// <summary>Lists all networks with pagination support.</summary>
    Task<List<Network>> ListAsync(string? nextToken);

    /// <summary>Retrieves a user's membership record for a specific network. Returns null if not a member.</summary>
    Task<NetworkMembership?> GetMembershipAsync(string networkId, string userId);

    /// <summary>Adds a new membership record for a user in a network.</summary>
    Task AddMemberAsync(NetworkMembership membership);

    /// <summary>Updates an existing membership record (role, status, contribution score).</summary>
    Task UpdateMemberAsync(NetworkMembership membership);

    /// <summary>Lists members of a network with pagination support.</summary>
    Task<List<NetworkMembership>> ListMembersAsync(string networkId, string? nextToken);

    /// <summary>Lists all networks a user belongs to via GSI-UserNetworks. Supports pagination.</summary>
    Task<List<Network>> ListUserNetworksAsync(string userId, string? nextToken);
}

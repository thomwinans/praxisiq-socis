using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Repositories;

/// <summary>
/// Read-only network repository for the Transaction service.
/// Queries the snapp-networks table to verify membership for referrals and attestations.
/// </summary>
public class NetworkReadRepository : INetworkRepository
{
    private readonly IAmazonDynamoDB _db;

    public NetworkReadRepository(IAmazonDynamoDB db) => _db = db;

    public async Task<NetworkMembership?> GetMembershipAsync(string networkId, string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"MEMBER#{userId}"),
            },
        });

        if (!response.IsItemSet) return null;

        return new NetworkMembership
        {
            NetworkId = networkId,
            UserId = userId,
            Role = response.Item.GetValueOrDefault("Role")?.S ?? "member",
            Status = Enum.TryParse<MembershipStatus>(response.Item.GetValueOrDefault("Status")?.S, out var s) ? s : MembershipStatus.Active,
            JoinedAt = DateTime.TryParse(response.Item.GetValueOrDefault("JoinedAt")?.S, out var j) ? j : DateTime.UtcNow,
        };
    }

    public async Task<List<Network>> ListUserNetworksAsync(string userId, string? nextToken)
    {
        // Query GSI-UserNetworks for networks a user belongs to
        var request = new QueryRequest
        {
            TableName = TableNames.Networks,
            IndexName = GsiNames.UserNetworks,
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.UserMembership}{userId}"),
            },
            Limit = 50,
        };

        var response = await _db.QueryAsync(request);
        return response.Items.Select(item => new Network
        {
            NetworkId = item.GetValueOrDefault("NetworkId")?.S ?? string.Empty,
            Name = item.GetValueOrDefault("Name")?.S ?? string.Empty,
        }).ToList();
    }

    // Not used by Transaction service — throw if called unexpectedly
    public Task<Network?> GetByIdAsync(string networkId) => throw new NotImplementedException("Read-only repository");
    public Task CreateAsync(Network network) => throw new NotImplementedException("Read-only repository");
    public Task UpdateAsync(Network network) => throw new NotImplementedException("Read-only repository");
    public Task<List<Network>> ListAsync(string? nextToken) => throw new NotImplementedException("Read-only repository");
    public Task AddMemberAsync(NetworkMembership membership) => throw new NotImplementedException("Read-only repository");
    public Task UpdateMemberAsync(NetworkMembership membership) => throw new NotImplementedException("Read-only repository");
    public Task<List<NetworkMembership>> ListMembersAsync(string networkId, string? nextToken) => throw new NotImplementedException("Read-only repository");
}

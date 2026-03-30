using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Network.Repositories;

public class NetworkRepository : INetworkRepository
{
    private readonly IAmazonDynamoDB _db;

    public NetworkRepository(IAmazonDynamoDB db)
    {
        _db = db;
    }

    // ── INetworkRepository implementation ──────────────────────────

    public async Task<Shared.Models.Network?> GetByIdAsync(string networkId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
        });

        if (!response.IsItemSet) return null;

        return MapNetwork(response.Item);
    }

    public async Task CreateAsync(Shared.Models.Network network)
    {
        var item = BuildNetworkItem(network);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK)",
        });
    }

    public async Task UpdateAsync(Shared.Models.Network network)
    {
        var item = BuildNetworkItem(network);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = item,
        });
    }

    public async Task<List<Shared.Models.Network>> ListAsync(string? nextToken)
    {
        // Scan for all META items (networks are META sort-key items)
        var request = new ScanRequest
        {
            TableName = TableNames.Networks,
            FilterExpression = "SK = :meta",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":meta"] = new(SortKeyValues.Meta),
            },
            Limit = 20,
        };

        var response = await _db.ScanAsync(request);
        return response.Items.Select(MapNetwork).ToList();
    }

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

        return MapMembership(response.Item);
    }

    public async Task AddMemberAsync(NetworkMembership membership)
    {
        var memberItem = BuildMemberItem(membership);

        // Inverse entry for user's networks query (GSI-UserNetworks)
        var umemItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.UserMembership}{membership.UserId}"),
            ["SK"] = new($"{KeyPrefixes.Network}{membership.NetworkId}"),
            ["GSI1PK"] = new($"{KeyPrefixes.UserMembership}{membership.UserId}"),
            ["GSI1SK"] = new($"{KeyPrefixes.Network}{membership.NetworkId}"),
            ["NetworkId"] = new(membership.NetworkId),
            ["UserId"] = new(membership.UserId),
            ["Role"] = new(membership.Role),
            ["Status"] = new(membership.Status.ToString()),
            ["JoinedAt"] = new(membership.JoinedAt.ToString("O")),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Networks, Item = memberItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Networks, Item = umemItem } },
            ],
        });
    }

    public async Task UpdateMemberAsync(NetworkMembership membership)
    {
        var item = BuildMemberItem(membership);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = item,
        });
    }

    public async Task<List<NetworkMembership>> ListMembersAsync(string networkId, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Networks,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Network}{networkId}"),
                [":prefix"] = new("MEMBER#"),
            },
            Limit = 50,
        };

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapMembership).ToList();
    }

    public async Task<List<Shared.Models.Network>> ListUserNetworksAsync(string userId, string? nextToken)
    {
        // Query UMEM# items for this user
        var request = new QueryRequest
        {
            TableName = TableNames.Networks,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.UserMembership}{userId}"),
                [":prefix"] = new(KeyPrefixes.Network),
            },
            Limit = 50,
        };

        try
        {
            var response = await _db.QueryAsync(request);

            // Fetch full network metadata for each membership
            var networks = new List<Shared.Models.Network>();
            foreach (var item in response.Items)
            {
                var networkId = item["NetworkId"].S;
                var network = await GetByIdAsync(networkId);
                if (network is not null)
                    networks.Add(network);
            }

            return networks;
        }
        catch (AmazonDynamoDBException)
        {
            return [];
        }
    }

    // ── Application methods (not part of INetworkRepository) ──────

    public async Task CreateApplicationAsync(string networkId, string userId, string? applicationText)
    {
        var now = DateTime.UtcNow;
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
            ["SK"] = new($"APP#{userId}"),
            ["NetworkId"] = new(networkId),
            ["UserId"] = new(userId),
            ["Status"] = new("Pending"),
            ["CreatedAt"] = new(now.ToString("O")),
            // GSI-PendingApps
            ["GSI2PK"] = new($"{KeyPrefixes.Network}{networkId}#PENDING"),
            ["GSI2SK"] = new($"APP#{userId}"),
        };

        if (applicationText is not null)
            item["ApplicationText"] = new(applicationText);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK) OR attribute_not_exists(SK)",
        });
    }

    public async Task<Dictionary<string, AttributeValue>?> GetApplicationAsync(string networkId, string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"APP#{userId}"),
            },
        });

        return response.IsItemSet ? response.Item : null;
    }

    public async Task<List<Dictionary<string, AttributeValue>>> ListPendingApplicationsAsync(string networkId)
    {
        // Query directly on the main table for APP# items with Pending status
        var request = new QueryRequest
        {
            TableName = TableNames.Networks,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "#status = :pending",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Network}{networkId}"),
                [":prefix"] = new("APP#"),
                [":pending"] = new("Pending"),
            },
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "Status",
            },
            Limit = 50,
        };

        var response = await _db.QueryAsync(request);
        return response.Items;
    }

    public async Task UpdateApplicationStatusAsync(string networkId, string userId, string status, string? reason)
    {
        var updateExpr = "SET #status = :status, DecidedAt = :now REMOVE GSI2PK, GSI2SK";
        var exprValues = new Dictionary<string, AttributeValue>
        {
            [":status"] = new(status),
            [":now"] = new(DateTime.UtcNow.ToString("O")),
        };

        if (reason is not null)
        {
            updateExpr = "SET #status = :status, DecidedAt = :now, Reason = :reason REMOVE GSI2PK, GSI2SK";
            exprValues[":reason"] = new(reason);
        }

        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"APP#{userId}"),
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "Status",
            },
            ExpressionAttributeValues = exprValues,
        });
    }

    // ── Role methods ──────────────────────────────────────────────

    public async Task CreateRoleAsync(string networkId, NetworkRole role)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
            ["SK"] = new($"ROLE#{role.RoleName}"),
            ["RoleName"] = new(role.RoleName),
            ["Permissions"] = new() { N = ((int)role.Permissions).ToString() },
        };

        if (role.Description is not null)
            item["Description"] = new(role.Description);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Networks,
            Item = item,
        });
    }

    public async Task<NetworkRole?> GetRoleAsync(string networkId, string roleName)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new($"ROLE#{roleName}"),
            },
        });

        if (!response.IsItemSet) return null;

        var item = response.Item;
        return new NetworkRole
        {
            RoleName = item["RoleName"].S,
            Permissions = (Permission)int.Parse(item["Permissions"].N),
            Description = item.TryGetValue("Description", out var desc) ? desc.S : null,
        };
    }

    // ── Member count ──────────────────────────────────────────────

    public async Task IncrementMemberCountAsync(string networkId)
    {
        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Networks,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Network}{networkId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
            UpdateExpression = "SET MemberCount = MemberCount + :one",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":one"] = new() { N = "1" },
            },
        });
    }

    // ── Private helpers ───────────────────────────────────────────

    private static Dictionary<string, AttributeValue> BuildNetworkItem(Shared.Models.Network network)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Network}{network.NetworkId}"),
            ["SK"] = new(SortKeyValues.Meta),
            ["NetworkId"] = new(network.NetworkId),
            ["Name"] = new(network.Name),
            ["CreatedByUserId"] = new(network.CreatedByUserId),
            ["MemberCount"] = new() { N = network.MemberCount.ToString() },
            ["CreatedAt"] = new(network.CreatedAt.ToString("O")),
        };

        if (network.Description is not null) item["Description"] = new(network.Description);
        if (network.Charter is not null) item["Charter"] = new(network.Charter);

        return item;
    }

    private static Dictionary<string, AttributeValue> BuildMemberItem(NetworkMembership membership)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Network}{membership.NetworkId}"),
            ["SK"] = new($"MEMBER#{membership.UserId}"),
            ["NetworkId"] = new(membership.NetworkId),
            ["UserId"] = new(membership.UserId),
            ["Role"] = new(membership.Role),
            ["Status"] = new(membership.Status.ToString()),
            ["JoinedAt"] = new(membership.JoinedAt.ToString("O")),
            ["ContributionScore"] = new() { N = membership.ContributionScore.ToString() },
        };
    }

    private static Shared.Models.Network MapNetwork(Dictionary<string, AttributeValue> item) => new()
    {
        NetworkId = item["NetworkId"].S,
        Name = item["Name"].S,
        Description = item.TryGetValue("Description", out var desc) ? desc.S : null,
        Charter = item.TryGetValue("Charter", out var charter) ? charter.S : null,
        CreatedByUserId = item["CreatedByUserId"].S,
        MemberCount = int.Parse(item["MemberCount"].N),
        CreatedAt = DateTime.Parse(item["CreatedAt"].S),
    };

    private static NetworkMembership MapMembership(Dictionary<string, AttributeValue> item) => new()
    {
        NetworkId = item["NetworkId"].S,
        UserId = item["UserId"].S,
        Role = item["Role"].S,
        Status = Enum.Parse<MembershipStatus>(item["Status"].S),
        JoinedAt = DateTime.Parse(item["JoinedAt"].S),
        ContributionScore = item.TryGetValue("ContributionScore", out var cs) ? decimal.Parse(cs.N) : 0,
    };
}

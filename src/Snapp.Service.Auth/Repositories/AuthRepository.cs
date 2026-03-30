using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Auth.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly IAmazonDynamoDB _db;

    public AuthRepository(IAmazonDynamoDB db)
    {
        _db = db;
    }

    public async Task CreateMagicLinkAsync(MagicLinkToken token)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Token}{token.Code}"),
            ["SK"] = new(SortKeyValues.MagicLink),
            ["HashedEmail"] = new(token.HashedEmail),
            ["CreatedAt"] = new(token.CreatedAt.ToString("O")),
            ["ExpiresAt"] = new() { N = new DateTimeOffset(token.ExpiresAt).ToUnixTimeSeconds().ToString() },
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    public async Task<MagicLinkToken?> GetMagicLinkAsync(string code)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Token}{code}"),
                ["SK"] = new(SortKeyValues.MagicLink),
            },
        });

        if (!response.IsItemSet) return null;

        var item = response.Item;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["ExpiresAt"].N)).UtcDateTime;

        if (expiresAt < DateTime.UtcNow) return null;

        return new MagicLinkToken
        {
            Code = code,
            HashedEmail = item["HashedEmail"].S,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            ExpiresAt = expiresAt,
        };
    }

    public async Task DeleteMagicLinkAsync(string code)
    {
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Token}{code}"),
                ["SK"] = new(SortKeyValues.MagicLink),
            },
        });
    }

    public async Task CreateRefreshTokenAsync(RefreshToken token)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Refresh}{token.TokenHash}"),
            ["SK"] = new(SortKeyValues.Session),
            ["UserId"] = new(token.UserId),
            ["CreatedAt"] = new(token.CreatedAt.ToString("O")),
            ["ExpiresAt"] = new() { N = new DateTimeOffset(token.ExpiresAt).ToUnixTimeSeconds().ToString() },
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Refresh}{tokenHash}"),
                ["SK"] = new(SortKeyValues.Session),
            },
        });

        if (!response.IsItemSet) return null;

        var item = response.Item;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["ExpiresAt"].N)).UtcDateTime;

        if (expiresAt < DateTime.UtcNow) return null;

        return new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = item["UserId"].S,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            ExpiresAt = expiresAt,
        };
    }

    public async Task DeleteRefreshTokenAsync(string tokenHash)
    {
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Refresh}{tokenHash}"),
                ["SK"] = new(SortKeyValues.Session),
            },
        });
    }

    public async Task<bool> TryIncrementRateLimitAsync(string hashedEmail, string windowKey, int maxRequests)
    {
        var pk = $"{KeyPrefixes.Rate}{hashedEmail}#MAGIC";
        var sk = $"WINDOW#{windowKey}";
        var ttl = DateTimeOffset.UtcNow.AddMinutes(Limits.RateLimitWindowMinutes).ToUnixTimeSeconds();

        try
        {
            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = TableNames.Users,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new(pk),
                    ["SK"] = new(sk),
                },
                UpdateExpression = "SET #count = if_not_exists(#count, :zero) + :one, ExpiresAt = :ttl",
                ConditionExpression = "attribute_not_exists(#count) OR #count < :max",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#count"] = "Count",
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":zero"] = new() { N = "0" },
                    [":one"] = new() { N = "1" },
                    [":max"] = new() { N = maxRequests.ToString() },
                    [":ttl"] = new() { N = ttl.ToString() },
                },
            });

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}

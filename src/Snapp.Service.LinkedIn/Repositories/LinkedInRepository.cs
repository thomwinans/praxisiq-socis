using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.LinkedIn.Repositories;

public class LinkedInRepository : ILinkedInRepository
{
    private readonly IAmazonDynamoDB _db;

    public LinkedInRepository(IAmazonDynamoDB db)
    {
        _db = db;
    }

    /// <summary>
    /// Atomic rate limiter using DynamoDB conditional writes.
    /// Reuses the same pattern as AuthRepository.TryIncrementRateLimitAsync.
    /// </summary>
    public async Task<bool> TryIncrementRateLimitAsync(string userId, string windowKey, int maxRequests)
    {
        var pk = $"{KeyPrefixes.Rate}{userId}#LINKEDIN";
        var sk = $"WINDOW#{windowKey}";
        var ttl = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds();

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

    public async Task SaveAsync(LinkedInToken token)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.User}{token.UserId}"),
            ["SK"] = new(SortKeyValues.LinkedIn),
            ["EncryptedLinkedInURN"] = new(token.EncryptedLinkedInURN),
            ["EncryptedAccessToken"] = new(token.EncryptedAccessToken),
            ["TokenExpiry"] = new(token.TokenExpiry.ToString("O")),
            ["EncryptionKeyId"] = new(token.EncryptionKeyId),
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    public async Task<LinkedInToken?> GetAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.LinkedIn),
            },
        });

        if (!response.IsItemSet) return null;

        var item = response.Item;
        return new LinkedInToken
        {
            UserId = userId,
            EncryptedLinkedInURN = item["EncryptedLinkedInURN"].S,
            EncryptedAccessToken = item["EncryptedAccessToken"].S,
            TokenExpiry = DateTime.Parse(item["TokenExpiry"].S),
            EncryptionKeyId = item["EncryptionKeyId"].S,
        };
    }

    public async Task DeleteAsync(string userId)
    {
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.LinkedIn),
            },
        });
    }
}

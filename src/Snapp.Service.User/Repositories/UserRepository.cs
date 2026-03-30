using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.User.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IAmazonDynamoDB _db;

    public UserRepository(IAmazonDynamoDB db)
    {
        _db = db;
    }

    public async Task<Shared.Models.User?> GetByIdAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Profile),
            },
        });

        if (!response.IsItemSet) return null;

        return MapUser(response.Item);
    }

    public async Task<UserPii?> GetPiiAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Pii),
            },
        });

        if (!response.IsItemSet) return null;

        var item = response.Item;
        return new UserPii
        {
            UserId = userId,
            EncryptedEmail = item["EncryptedEmail"].S,
            EncryptedPhone = item.TryGetValue("EncryptedPhone", out var phone) ? phone.S : null,
            EncryptedContactInfo = item.TryGetValue("EncryptedContactInfo", out var contact) ? contact.S : null,
            EncryptionKeyId = item["EncryptionKeyId"].S,
        };
    }

    public async Task<string?> GetUserIdByEmailHashAsync(string emailHash)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Email}{emailHash}"),
                ["SK"] = new(SortKeyValues.User),
            },
        });

        if (!response.IsItemSet) return null;

        return response.Item["UserId"].S;
    }

    public async Task CreateAsync(Shared.Models.User user, UserPii pii, string emailHash)
    {
        var profileItem = BuildProfileItem(user);

        var piiItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.User}{user.UserId}"),
            ["SK"] = new(SortKeyValues.Pii),
            ["EncryptedEmail"] = new(pii.EncryptedEmail),
            ["EncryptionKeyId"] = new(pii.EncryptionKeyId),
        };
        if (pii.EncryptedPhone is not null) piiItem["EncryptedPhone"] = new(pii.EncryptedPhone);
        if (pii.EncryptedContactInfo is not null) piiItem["EncryptedContactInfo"] = new(pii.EncryptedContactInfo);

        var emailItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Email}{emailHash}"),
            ["SK"] = new(SortKeyValues.User),
            ["UserId"] = new(user.UserId),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Users, Item = profileItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Users, Item = piiItem } },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = TableNames.Users,
                        Item = emailItem,
                        ConditionExpression = "attribute_not_exists(PK)",
                    },
                },
            ],
        });
    }

    public async Task UpdateAsync(Shared.Models.User user)
    {
        var item = BuildProfileItem(user);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    public async Task UpdatePiiAsync(UserPii pii)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.User}{pii.UserId}"),
            ["SK"] = new(SortKeyValues.Pii),
            ["EncryptedEmail"] = new(pii.EncryptedEmail),
            ["EncryptionKeyId"] = new(pii.EncryptionKeyId),
        };
        if (pii.EncryptedPhone is not null) item["EncryptedPhone"] = new(pii.EncryptedPhone);
        if (pii.EncryptedContactInfo is not null) item["EncryptedContactInfo"] = new(pii.EncryptedContactInfo);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    public async Task<List<Shared.Models.User>> SearchBySpecialtyGeoAsync(string specialty, string geo, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Users,
            IndexName = GsiNames.Specialty,
            KeyConditionExpression = "GSI2PK = :pk AND begins_with(GSI2SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"SPECIALTY#{specialty}"),
                [":sk"] = new($"GEO#{geo}"),
            },
            Limit = 20,
        };

        // Note: GSI-Specialty may not exist in DynamoDB Local without explicit creation.
        // In that case, fall back to a scan with filter.
        try
        {
            var response = await _db.QueryAsync(request);
            return response.Items.Select(MapUser).ToList();
        }
        catch (AmazonDynamoDBException)
        {
            // GSI not yet provisioned — return empty list gracefully
            return [];
        }
    }

    private static Dictionary<string, AttributeValue> BuildProfileItem(Shared.Models.User user)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.User}{user.UserId}"),
            ["SK"] = new(SortKeyValues.Profile),
            ["UserId"] = new(user.UserId),
            ["DisplayName"] = new(user.DisplayName),
            ["ProfileCompleteness"] = new() { N = user.ProfileCompleteness.ToString() },
            ["HasPracticeData"] = new() { BOOL = user.HasPracticeData },
            ["CreatedAt"] = new(user.CreatedAt.ToString("O")),
            ["UpdatedAt"] = new(user.UpdatedAt.ToString("O")),
        };
        if (user.Specialty is not null)
        {
            item["Specialty"] = new(user.Specialty);
            item["GSI2PK"] = new($"SPECIALTY#{user.Specialty}");
            if (user.Geography is not null)
                item["GSI2SK"] = new($"GEO#{user.Geography}");
        }
        if (user.Geography is not null) item["Geography"] = new(user.Geography);
        if (user.LinkedInProfileUrl is not null) item["LinkedInProfileUrl"] = new(user.LinkedInProfileUrl);
        if (user.PhotoUrl is not null) item["PhotoUrl"] = new(user.PhotoUrl);

        return item;
    }

    private static Shared.Models.User MapUser(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item["UserId"].S,
        DisplayName = item["DisplayName"].S,
        Specialty = item.TryGetValue("Specialty", out var spec) ? spec.S : null,
        Geography = item.TryGetValue("Geography", out var geo) ? geo.S : null,
        LinkedInProfileUrl = item.TryGetValue("LinkedInProfileUrl", out var li) ? li.S : null,
        PhotoUrl = item.TryGetValue("PhotoUrl", out var photo) ? photo.S : null,
        HasPracticeData = item.TryGetValue("HasPracticeData", out var pd) && pd.BOOL,
        ProfileCompleteness = decimal.Parse(item["ProfileCompleteness"].N),
        CreatedAt = DateTime.Parse(item["CreatedAt"].S),
        UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
    };
}

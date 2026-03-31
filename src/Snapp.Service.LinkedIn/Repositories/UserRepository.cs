using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.LinkedIn.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IAmazonDynamoDB _db;

    public UserRepository(IAmazonDynamoDB db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(string userId)
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

    public async Task UpdateAsync(User user)
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
        if (user.Specialty is not null) item["Specialty"] = new(user.Specialty);
        if (user.Geography is not null) item["Geography"] = new(user.Geography);
        if (user.LinkedInProfileUrl is not null) item["LinkedInProfileUrl"] = new(user.LinkedInProfileUrl);
        if (user.PhotoUrl is not null) item["PhotoUrl"] = new(user.PhotoUrl);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });
    }

    // Methods not needed by the LinkedIn service — throw NotImplementedException
    public Task<UserPii?> GetPiiAsync(string userId) =>
        throw new NotImplementedException("Not needed by LinkedIn service");

    public Task<string?> GetUserIdByEmailHashAsync(string emailHash) =>
        throw new NotImplementedException("Not needed by LinkedIn service");

    public Task CreateAsync(User user, UserPii pii, string emailHash) =>
        throw new NotImplementedException("Not needed by LinkedIn service");

    public Task UpdatePiiAsync(UserPii pii) =>
        throw new NotImplementedException("Not needed by LinkedIn service");

    public Task<List<User>> SearchBySpecialtyGeoAsync(string specialty, string geo, string? nextToken) =>
        throw new NotImplementedException("Not needed by LinkedIn service");

    private static User MapUser(Dictionary<string, AttributeValue> item) => new()
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

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;

namespace Snapp.TestHelpers;

/// <summary>
/// Helper for creating and cleaning up test data in DynamoDB Local.
/// </summary>
public sealed class DynamoDbTestHelper : IDisposable
{
    private readonly AmazonDynamoDBClient _client;

    public AmazonDynamoDBClient Client => _client;

    public DynamoDbTestHelper(string serviceUrl = "http://localhost:8042")
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey",
            "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
    }

    /// <summary>
    /// Ensures the snapp-users table exists. Creates it if not present.
    /// </summary>
    public async Task EnsureUsersTableAsync()
    {
        try
        {
            await _client.DescribeTableAsync(TableNames.Users);
        }
        catch (ResourceNotFoundException)
        {
            try
            {
                await _client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = TableNames.Users,
                    KeySchema =
                    [
                        new KeySchemaElement("PK", KeyType.HASH),
                        new KeySchemaElement("SK", KeyType.RANGE)
                    ],
                    AttributeDefinitions =
                    [
                        new AttributeDefinition("PK", ScalarAttributeType.S),
                        new AttributeDefinition("SK", ScalarAttributeType.S)
                    ],
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });
            }
            catch (ResourceInUseException)
            {
                // Another test created the table concurrently — safe to ignore
            }
        }
    }

    /// <summary>
    /// Inserts a minimal user record and returns the userId (ULID).
    /// </summary>
    public async Task<string> CreateTestUserAsync()
    {
        await EnsureUsersTableAsync();

        var userId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("o");

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new("PROFILE"),
                ["UserId"] = new(userId),
                ["DisplayName"] = new($"Test User {userId[..8]}"),
                ["CreatedAt"] = new(now),
                ["UpdatedAt"] = new(now),
                ["ProfileCompleteness"] = new() { N = "0" }
            }
        });

        return userId;
    }

    /// <summary>
    /// Deletes a specific item from DynamoDB Local.
    /// </summary>
    public async Task CleanupAsync(string tableName, string pk, string sk)
    {
        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new(pk),
                ["SK"] = new(sk)
            }
        });
    }

    /// <summary>
    /// Deletes the entire table if it exists. Useful for full test isolation.
    /// </summary>
    public async Task DeleteTableIfExistsAsync(string tableName)
    {
        try
        {
            await _client.DeleteTableAsync(tableName);
        }
        catch (ResourceNotFoundException)
        {
            // Already gone
        }
    }

    public void Dispose() => _client.Dispose();
}

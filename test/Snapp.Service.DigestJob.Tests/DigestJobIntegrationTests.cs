using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.DigestJob.Services;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.DigestJob.Tests;

[Collection(DockerTestCollection.Name)]
public class DigestJobIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly DynamoDbTestHelper _dynamo;
    private readonly PapercutClient _papercut;
    private readonly byte[] _masterKey;
    private readonly LocalFileEncryptor _encryptor;

    public DigestJobIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _dynamo = new DynamoDbTestHelper();
        _papercut = new PapercutClient(fixture.PapercutUrl);
        _masterKey = RandomNumberGenerator.GetBytes(32);
        _encryptor = new LocalFileEncryptor(_masterKey);
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureNotificationsTableAsync();

        // Do NOT clear Papercut inbox — other test assemblies (Auth, User, Network)
        // run in parallel and rely on emails being present for magic link extraction.
    }

    public Task DisposeAsync()
    {
        _dynamo.Dispose();
        _papercut.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RunDigest_WithUndigestedNotifications_SendsEmailGroupedByCategory()
    {
        var userId = Ulid.NewUlid().ToString();
        var email = $"digest-test-{Guid.NewGuid():N}@test.snapp";
        var digestHour = "14";

        // Setup: create user PII with encrypted email
        await InsertUserPii(userId, email);
        await InsertDigestQueueEntry(userId, digestHour);

        // Insert notifications of different types
        await InsertNotification(userId, "New referral from Dr. Smith", "Referral for patient case", NotificationType.ReferralReceived);
        await InsertNotification(userId, "Referral outcome recorded", "Case completed successfully", NotificationType.ReferralOutcome);
        await InsertNotification(userId, "You were mentioned", "In the Q1 revenue discussion", NotificationType.MentionInDiscussion);
        await InsertNotification(userId, "New member joined", "Dr. Johnson joined your network", NotificationType.NewNetworkMember);
        await InsertNotification(userId, "Valuation updated", "Your practice score changed", NotificationType.ValuationChanged);

        // Run digest
        var processor = CreateProcessor();
        await processor.RunAsync(digestHour);

        // Verify email was sent to Papercut
        var messages = await _papercut.WaitForMessagesAsync(email, maxRetries: 10, delayMs: 500);
        messages.Should().NotBeEmpty("digest email should have been sent");

        var digestEmail = messages.First();
        digestEmail.Subject.Should().Contain("PraxisIQ Daily Digest");
        digestEmail.Body.Should().Contain("Referrals");
        digestEmail.Body.Should().Contain("Discussions");
        digestEmail.Body.Should().Contain("Network");
        digestEmail.Body.Should().Contain("Intelligence");
        digestEmail.Body.Should().Contain("New referral from Dr. Smith");
        digestEmail.Body.Should().Contain("You were mentioned");
        digestEmail.Body.Should().Contain("New member joined");
        digestEmail.Body.Should().Contain("Valuation updated");
    }

    [Fact]
    public async Task RunDigest_WithUndigestedNotifications_MarksAsDigested()
    {
        var userId = Ulid.NewUlid().ToString();
        var email = $"digest-mark-{Guid.NewGuid():N}@test.snapp";
        var digestHour = "15";

        await InsertUserPii(userId, email);
        await InsertDigestQueueEntry(userId, digestHour);
        var notifId = await InsertNotification(userId, "Test digest mark", "Body", NotificationType.MentionInDiscussion);

        var processor = CreateProcessor();
        await processor.RunAsync(digestHour);

        // Wait for email to confirm send happened
        var messages = await _papercut.WaitForMessagesAsync(email, maxRetries: 10, delayMs: 500);
        messages.Should().NotBeEmpty();

        // Verify notification marked as digested
        var notifs = await QueryNotifications(userId);
        var notification = notifs.FirstOrDefault(n => n["NotificationId"].S == notifId);
        notification.Should().NotBeNull();
        notification!["IsDigested"].BOOL.Should().BeTrue();
    }

    [Fact]
    public async Task RunDigest_NoUndigestedNotifications_SendsNoEmail()
    {
        var userId = Ulid.NewUlid().ToString();
        var email = $"digest-empty-{Guid.NewGuid():N}@test.snapp";
        var digestHour = "16";

        await InsertUserPii(userId, email);
        await InsertDigestQueueEntry(userId, digestHour);
        // No notifications inserted

        var processor = CreateProcessor();
        await processor.RunAsync(digestHour);

        // Brief wait then check — no email should arrive
        await Task.Delay(1000);
        var messages = await _papercut.GetMessagesForRecipientAsync(email);
        messages.Should().BeEmpty("no digest should be sent when there are no notifications");
    }

    [Fact]
    public async Task RunDigest_RecordsDigestSentItem()
    {
        var userId = Ulid.NewUlid().ToString();
        var email = $"digest-record-{Guid.NewGuid():N}@test.snapp";
        var digestHour = "17";

        await InsertUserPii(userId, email);
        await InsertDigestQueueEntry(userId, digestHour);
        await InsertNotification(userId, "Record test", "Body", NotificationType.ReferralReceived);

        var processor = CreateProcessor();
        await processor.RunAsync(digestHour);

        // Wait for email
        var messages = await _papercut.WaitForMessagesAsync(email, maxRetries: 10, delayMs: 500);
        messages.Should().NotBeEmpty();

        // Verify DIGEST# record
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var response = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Notifications,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Digest}{userId}"),
                ["SK"] = new($"SENT#{today}"),
            },
        });

        response.Item.Should().NotBeNull();
        response.Item.Should().ContainKey("Count");
        int.Parse(response.Item["Count"].N).Should().BeGreaterThan(0);
        response.Item["Categories"].SS.Should().Contain("Referrals");
    }

    [Fact]
    public async Task RunDigest_NoUsersForHour_CompletesWithoutError()
    {
        var processor = CreateProcessor();
        // Use an hour that has no users queued
        await processor.RunAsync("23");
        // Should complete without throwing
    }

    [Fact]
    public async Task RunDigest_UserWithNoEmail_SkipsGracefully()
    {
        var userId = Ulid.NewUlid().ToString();
        var digestHour = "18";

        // Queue user but do NOT insert PII
        await InsertDigestQueueEntry(userId, digestHour);
        await InsertNotification(userId, "No email test", "Body", NotificationType.MentionInDiscussion);

        var processor = CreateProcessor();
        await processor.RunAsync(digestHour);

        // Notification should NOT be marked as digested (no email was sent)
        var notifs = await QueryNotifications(userId);
        notifs.Should().AllSatisfy(n => n["IsDigested"].BOOL.Should().BeFalse());
    }

    [Fact]
    public void GroupByCategory_GroupsCorrectly()
    {
        var notifications = new List<Snapp.Shared.Models.Notification>
        {
            new() { NotificationId = "1", Type = NotificationType.ReferralReceived, Title = "R1", Body = "B" },
            new() { NotificationId = "2", Type = NotificationType.ReferralOutcome, Title = "R2", Body = "B" },
            new() { NotificationId = "3", Type = NotificationType.MentionInDiscussion, Title = "D1", Body = "B" },
            new() { NotificationId = "4", Type = NotificationType.ApplicationReceived, Title = "N1", Body = "B" },
            new() { NotificationId = "5", Type = NotificationType.NewNetworkMember, Title = "N2", Body = "B" },
            new() { NotificationId = "6", Type = NotificationType.ValuationChanged, Title = "I1", Body = "B" },
            new() { NotificationId = "7", Type = NotificationType.MilestoneAchieved, Title = "I2", Body = "B" },
        };

        var grouped = DigestProcessor.GroupByCategory(notifications);

        grouped.Should().ContainKey("Referrals").WhoseValue.Should().HaveCount(2);
        grouped.Should().ContainKey("Discussions").WhoseValue.Should().HaveCount(1);
        grouped.Should().ContainKey("Network").WhoseValue.Should().HaveCount(2);
        grouped.Should().ContainKey("Intelligence").WhoseValue.Should().HaveCount(2);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private DigestProcessor CreateProcessor()
    {
        var emailSender = new SmtpEmailSender(
            _fixture.PapercutSmtpHost,
            _fixture.PapercutSmtpPort);

        return new DigestProcessor(
            _dynamo.Client,
            _encryptor,
            emailSender,
            NullLogger<DigestProcessor>.Instance);
    }

    private async Task InsertUserPii(string userId, string email)
    {
        var (encryptedEmail, keyId) = await _encryptor.EncryptWithKeyIdAsync(email);

        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Pii),
                ["UserId"] = new(userId),
                ["EncryptedEmail"] = new(encryptedEmail),
                ["EncryptionKeyId"] = new(keyId),
            },
        });
    }

    private async Task InsertDigestQueueEntry(string userId, string digestHour)
    {
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.DigestQueue}{digestHour}"),
                ["SK"] = new($"{KeyPrefixes.User}{userId}"),
                ["Timezone"] = new("America/New_York"),
                ["PreferredTime"] = new($"{digestHour}:00"),
            },
        });
    }

    private async Task<string> InsertNotification(
        string userId, string title, string body, NotificationType type)
    {
        var notifId = Ulid.NewUlid().ToString();
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Notification}{userId}"),
                ["SK"] = new($"EVENT#{timestamp}#{notifId}"),
                ["NotificationId"] = new(notifId),
                ["UserId"] = new(userId),
                ["Type"] = new(type.ToString()),
                ["Title"] = new(title),
                ["Body"] = new(body),
                ["IsRead"] = new() { BOOL = false },
                ["IsDigested"] = new() { BOOL = false },
                ["CreatedAt"] = new(timestamp),
            },
        });

        return notifId;
    }

    private async Task<List<Dictionary<string, AttributeValue>>> QueryNotifications(string userId)
    {
        var response = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
            },
        });
        return response.Items;
    }

    private async Task EnsureNotificationsTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Notifications);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Notifications,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }
}

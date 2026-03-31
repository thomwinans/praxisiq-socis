using System.Net;
using System.Net.Mail;
using Xunit;

namespace Snapp.TestHelpers.Tests;

[Collection(DockerTestCollection.Name)]
public class PapercutClientTests
{
    private readonly DockerTestFixture _fixture;

    public PapercutClientTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SendAndRetrieve_RoundTrip_MatchesSubjectAndBody()
    {
        // Arrange
        var recipient = $"test-{Guid.NewGuid():N}@example.com";
        var subject = $"Test Email {Guid.NewGuid():N}";
        var body = "<html><body><p>Hello from the round-trip test!</p></body></html>";

        // Act — send via SMTP
        using var smtp = new SmtpClient(_fixture.PapercutSmtpHost, _fixture.PapercutSmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var message = new MailMessage("noreply@snapp.test", recipient, subject, body)
        {
            IsBodyHtml = true
        };

        await smtp.SendMailAsync(message);

        // Act — retrieve via PapercutClient (polls until message arrives)
        var messages = await _fixture.PapercutClient.WaitForMessagesAsync(recipient);

        // Assert
        Assert.Single(messages);
        var retrieved = messages[0];
        Assert.Contains(subject, retrieved.Subject);
        Assert.Contains("Hello from the round-trip test!", retrieved.Body);
    }

    [Fact]
    public async Task ExtractMagicLinkCode_ReturnsCode_WhenPresent()
    {
        // Arrange
        var recipient = $"magic-{Guid.NewGuid():N}@example.com";
        var code = "test_magic_link_code_ABC123-xyz";
        var body = $"<html><body><a href=\"https://app.snapp.test/auth/verify?code={code}\">Click here</a></body></html>";

        using var smtp = new SmtpClient(_fixture.PapercutSmtpHost, _fixture.PapercutSmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var message = new MailMessage("noreply@snapp.test", recipient, "Your Magic Link", body)
        {
            IsBodyHtml = true
        };

        await smtp.SendMailAsync(message);

        // Act — ExtractMagicLinkCodeAsync polls with retries internally
        var extractedCode = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(recipient, maxRetries: 10, delayMs: 500);

        // Assert
        Assert.Equal(code, extractedCode);
    }

    [Fact(Skip = "Destructive: DeleteAllMessages wipes the shared Papercut inbox, causing race conditions with parallel integration tests")]
    public async Task DeleteAllMessages_ClearsInbox()
    {
        // Arrange — send an email
        using var smtp = new SmtpClient(_fixture.PapercutSmtpHost, _fixture.PapercutSmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        await smtp.SendMailAsync("noreply@snapp.test", "delete-test@example.com", "Delete Test", "body");
        await Task.Delay(500);

        // Act
        await _fixture.PapercutClient.DeleteAllMessagesAsync();
        await Task.Delay(500);

        var messages = await _fixture.PapercutClient.GetMessagesAsync();

        // Assert
        Assert.Empty(messages);
    }
}

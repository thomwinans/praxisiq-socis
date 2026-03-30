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

        // Clear inbox
        await _fixture.PapercutClient.DeleteAllMessagesAsync();

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

        // Brief delay for Papercut to process
        await Task.Delay(1000);

        // Act — retrieve via PapercutClient
        var messages = await _fixture.PapercutClient.GetMessagesForRecipientAsync(recipient);

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

        await _fixture.PapercutClient.DeleteAllMessagesAsync();

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
        await Task.Delay(1000);

        // Act
        var extractedCode = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(recipient);

        // Assert
        Assert.Equal(code, extractedCode);
    }

    [Fact]
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

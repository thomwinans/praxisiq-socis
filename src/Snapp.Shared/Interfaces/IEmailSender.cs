namespace Snapp.Shared.Interfaces;

/// <summary>
/// Abstracts email delivery. Implementations:
/// SmtpEmailSender (dev) — sends to Papercut SMTP on port 25.
/// SesEmailSender (prod) — sends via AWS SES v2.
/// Registered based on config Email:Provider.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email. The implementation determines the transport (SMTP or SES).
    /// </summary>
    /// <param name="toEmail">Recipient email address (plaintext — caller is responsible for having decrypted PII).</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="textBody">Optional plain-text fallback body.</param>
    Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null);
}

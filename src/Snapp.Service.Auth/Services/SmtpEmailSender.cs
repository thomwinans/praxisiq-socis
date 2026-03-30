using System.Net;
using System.Net.Mail;
using Snapp.Shared.Interfaces;

namespace Snapp.Service.Auth.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _fromAddress;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _host = configuration["Email:SmtpHost"] ?? "localhost";
        _port = int.Parse(configuration["Email:SmtpPort"] ?? "1025");
        _fromAddress = configuration["Email:FromAddress"] ?? "noreply@snapp.local";
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null)
    {
        using var message = new MailMessage();
        message.From = new MailAddress(_fromAddress, "SNAPP");
        message.To.Add(new MailAddress(toEmail));
        message.Subject = subject;
        message.IsBodyHtml = true;
        message.Body = htmlBody;

        if (textBody is not null)
        {
            var plainView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);
            message.Body = textBody;
            message.IsBodyHtml = false;
        }

        using var client = new SmtpClient(_host, _port);
        client.EnableSsl = false;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.Credentials = new NetworkCredential();

        await client.SendMailAsync(message);
    }
}

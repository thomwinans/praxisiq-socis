using System.Text;
using NotificationModel = Snapp.Shared.Models.Notification;

namespace Snapp.Service.DigestJob;

public static class DigestEmailRenderer
{
    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Referrals"] = "&#128279;",
        ["Discussions"] = "&#128172;",
        ["Network"] = "&#128101;",
        ["Intelligence"] = "&#128200;",
        ["General"] = "&#128276;",
    };

    private static readonly string[] CategoryOrder = ["Referrals", "Discussions", "Network", "Intelligence", "General"];

    public static string Render(string dateString, Dictionary<string, List<NotificationModel>> grouped)
    {
        var sb = new StringBuilder();

        sb.Append("""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            </head>
            <body style="margin:0; padding:0; background-color:#f4f4f7; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f7;">
            <tr><td align="center" style="padding: 24px 0;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background-color:#ffffff; border-radius:8px; overflow:hidden;">
            """);

        // Header
        sb.Append($"""
            <tr><td style="background-color:#1a1a2e; padding:32px 40px;">
            <h1 style="color:#ffffff; margin:0; font-size:24px; font-weight:600;">PraxisIQ Daily Digest</h1>
            <p style="color:#a0a0b0; margin:8px 0 0 0; font-size:14px;">{dateString}</p>
            </td></tr>
            """);

        // Summary
        var totalCount = grouped.Values.Sum(list => list.Count);
        sb.Append($"""
            <tr><td style="padding:24px 40px 8px 40px;">
            <p style="color:#333; font-size:16px; margin:0;">You have <strong>{totalCount}</strong> new notification{(totalCount != 1 ? "s" : "")} since your last digest.</p>
            </td></tr>
            """);

        // Category sections in priority order
        foreach (var category in CategoryOrder)
        {
            if (!grouped.TryGetValue(category, out var notifications))
                continue;

            var icon = CategoryIcons.GetValueOrDefault(category, "&#128276;");

            sb.Append($"""
                <tr><td style="padding:16px 40px 0 40px;">
                <h2 style="color:#1a1a2e; font-size:18px; margin:0 0 12px 0; border-bottom:2px solid #e8e8ed; padding-bottom:8px;">
                {icon} {category} ({notifications.Count})
                </h2>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                """);

            foreach (var n in notifications)
            {
                var timeAgo = FormatTimeAgo(n.CreatedAt);
                sb.Append($"""
                    <tr><td style="padding:8px 0; border-bottom:1px solid #f0f0f5;">
                    <p style="color:#333; font-size:14px; margin:0; font-weight:600;">{Escape(n.Title)}</p>
                    <p style="color:#666; font-size:13px; margin:4px 0 0 0;">{Escape(n.Body)}</p>
                    <p style="color:#999; font-size:12px; margin:4px 0 0 0;">{timeAgo}</p>
                    </td></tr>
                    """);
            }

            sb.Append("""
                </table>
                </td></tr>
                """);
        }

        // Footer
        sb.Append("""
            <tr><td style="padding:32px 40px; background-color:#f8f8fb; text-align:center;">
            <p style="color:#666; font-size:13px; margin:0;">
            <a href="#" style="color:#5c6bc0; text-decoration:none;">Manage notification preferences</a>
            </p>
            <p style="color:#999; font-size:12px; margin:8px 0 0 0;">PraxisIQ &mdash; Intelligence for Practice Owners</p>
            </td></tr>
            """);

        sb.Append("""
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string Escape(string text) =>
        System.Net.WebUtility.HtmlEncode(text);

    private static string FormatTimeAgo(DateTime createdAt)
    {
        var elapsed = DateTime.UtcNow - createdAt;
        return elapsed.TotalHours switch
        {
            < 1 => $"{(int)elapsed.TotalMinutes}m ago",
            < 24 => $"{(int)elapsed.TotalHours}h ago",
            _ => createdAt.ToString("MMM dd, h:mm tt") + " UTC",
        };
    }
}

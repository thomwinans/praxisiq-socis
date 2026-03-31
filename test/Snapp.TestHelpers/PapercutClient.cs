using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Snapp.TestHelpers;

/// <summary>
/// Typed HTTP client for Papercut SMTP's REST API (changemakerstudiosus/papercut-smtp v11+).
///   GET    /api/messages       — { totalMessageCount, messages: [{id, subject, createdAt}] }
///   GET    /api/messages/{id}  — full detail with from/to as [{name, address}], htmlBody, textBody
///   DELETE /api/messages       — delete all messages
/// Message IDs contain spaces and must be URL-encoded.
/// </summary>
public sealed class PapercutClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PapercutClient(string baseUrl = "http://localhost:8025")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public PapercutClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<List<EmailMessage>> GetMessagesAsync()
    {
        var response = await _http.GetAsync("/api/messages");
        response.EnsureSuccessStatusCode();

        var listing = await response.Content.ReadFromJsonAsync<PapercutMessageListing>(JsonOptions);
        var summaries = listing?.Messages ?? [];

        var result = new List<EmailMessage>();
        foreach (var summary in summaries)
        {
            var detail = await GetMessageDetailAsync(summary.Id);
            if (detail is not null)
                result.Add(detail);
        }

        return result;
    }

    public async Task<List<EmailMessage>> GetMessagesForRecipientAsync(string email)
    {
        // Only check the most recent messages to avoid N+1 query explosion
        // when the inbox is large from parallel test assemblies.
        var response = await _http.GetAsync("/api/messages");
        response.EnsureSuccessStatusCode();

        var listing = await response.Content.ReadFromJsonAsync<PapercutMessageListing>(JsonOptions);
        var summaries = (listing?.Messages ?? [])
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        var result = new List<EmailMessage>();
        foreach (var summary in summaries)
        {
            var detail = await GetMessageDetailAsync(summary.Id);
            if (detail is not null && detail.To.Contains(email, StringComparison.OrdinalIgnoreCase))
                result.Add(detail);
        }

        return result;
    }

    /// <summary>
    /// Polls Papercut until at least one message arrives for the given recipient.
    /// Returns the matching messages, or an empty list if none arrive within the timeout.
    /// </summary>
    public async Task<List<EmailMessage>> WaitForMessagesAsync(string email, int maxRetries = 10, int delayMs = 1000)
    {
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var messages = await GetMessagesForRecipientAsync(email);
            if (messages.Count > 0)
                return messages;

            if (attempt < maxRetries)
                await Task.Delay(delayMs);
        }

        return [];
    }

    public async Task DeleteAllMessagesAsync()
    {
        var response = await _http.DeleteAsync("/api/messages");
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> ExtractMagicLinkCodeAsync(string email, int maxRetries = 10, int delayMs = 1000)
    {
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            // Fetch summaries and check details one at a time (newest first),
            // breaking early on first recipient match to avoid N+1 explosion.
            var response = await _http.GetAsync("/api/messages");
            if (response.IsSuccessStatusCode)
            {
                var listing = await response.Content.ReadFromJsonAsync<PapercutMessageListing>(JsonOptions);
                var summaries = (listing?.Messages ?? [])
                    .OrderByDescending(s => s.CreatedAt);

                foreach (var summary in summaries)
                {
                    var detail = await GetMessageDetailAsync(summary.Id);
                    if (detail is null) continue;
                    if (!detail.To.Contains(email, StringComparison.OrdinalIgnoreCase)) continue;

                    var match = Regex.Match(detail.Body, @"code=([A-Za-z0-9_-]+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }

            if (attempt < maxRetries)
                await Task.Delay(delayMs);
        }

        return null;
    }

    private async Task<EmailMessage?> GetMessageDetailAsync(string id)
    {
        var encodedId = Uri.EscapeDataString(id);
        var response = await _http.GetAsync($"/api/messages/{encodedId}");

        // Message may have been deleted by a parallel test between list and detail fetch,
        // or Papercut may return a transient 500 under concurrent load — treat both as skip.
        if (!response.IsSuccessStatusCode)
            return null;

        var detail = await response.Content.ReadFromJsonAsync<PapercutMessageDetail>(JsonOptions);
        if (detail is null)
            return null;

        var fromAddr = detail.From?.FirstOrDefault()?.Address ?? "";
        var toAddrs = string.Join(", ", detail.To?.Select(t => t.Address ?? "") ?? []);

        return new EmailMessage
        {
            Id = id,
            From = fromAddr,
            To = toAddrs,
            Subject = detail.Subject ?? "",
            Body = detail.HtmlBody ?? detail.TextBody ?? "",
            ReceivedAt = detail.CreatedAt ?? DateTime.UtcNow
        };
    }

    public void Dispose() => _http.Dispose();

    // Internal DTOs matching Papercut's actual API shape
    private sealed class PapercutMessageListing
    {
        public int TotalMessageCount { get; set; }
        public List<PapercutMessageSummary> Messages { get; set; } = [];
    }

    private sealed class PapercutMessageSummary
    {
        public string Id { get; set; } = "";
        public string? Subject { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    private sealed class PapercutMessageDetail
    {
        public string Id { get; set; } = "";
        public string? Subject { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<PapercutAddress>? From { get; set; }
        public List<PapercutAddress>? To { get; set; }
        public List<PapercutAddress>? Cc { get; set; }
        [JsonPropertyName("bCc")]
        public List<PapercutAddress>? Bcc { get; set; }
        public string? HtmlBody { get; set; }
        public string? TextBody { get; set; }
    }

    private sealed class PapercutAddress
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
    }
}

public sealed class EmailMessage
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}

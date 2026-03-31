using System.Net.Http.Json;
using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.Models;

namespace Snapp.Client.Services;

public class NotificationService : INotificationService
{
    private readonly HttpClient _http;

    public NotificationService(HttpClient http)
    {
        _http = http;
    }

    public async Task<NotificationListResponse> ListAsync(int limit = 25, string? nextToken = null)
    {
        var url = $"notif?limit={limit}";
        if (!string.IsNullOrEmpty(nextToken))
            url += $"&nextToken={Uri.EscapeDataString(nextToken)}";

        return await _http.GetFromJsonAsync<NotificationListResponse>(url)
               ?? new NotificationListResponse();
    }

    public async Task<bool> MarkReadAsync(string notificationId)
    {
        var response = await _http.PostAsync($"notif/{notificationId}/read", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MarkAllReadAsync()
    {
        var response = await _http.PostAsync("notif/read-all", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<NotificationPreferences?> GetPreferencesAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<NotificationPreferences>("notif/preferences");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SavePreferencesAsync(UpdatePreferencesRequest request)
    {
        var response = await _http.PutAsJsonAsync("notif/preferences", request);
        return response.IsSuccessStatusCode;
    }
}

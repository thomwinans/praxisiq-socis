using System.Net.Http.Json;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public class DealRoomService : IDealRoomService
{
    private readonly HttpClient _http;

    public DealRoomService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DealRoomListResponse> GetDealRoomsAsync(string? nextToken = null)
    {
        var url = "tx/deals";
        if (!string.IsNullOrEmpty(nextToken))
            url += $"?nextToken={Uri.EscapeDataString(nextToken)}";
        return await _http.GetFromJsonAsync<DealRoomListResponse>(url)
               ?? new DealRoomListResponse();
    }

    public async Task<DealRoomResponse?> GetDealRoomAsync(string dealId)
    {
        return await _http.GetFromJsonAsync<DealRoomResponse>($"tx/deals/{dealId}");
    }

    public async Task<DealRoomResponse?> CreateAsync(CreateDealRoomRequest request)
    {
        var response = await _http.PostAsJsonAsync("tx/deals", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DealRoomResponse>();
        return null;
    }

    public async Task<List<DealParticipantResponse>> GetParticipantsAsync(string dealId)
    {
        return await _http.GetFromJsonAsync<List<DealParticipantResponse>>($"tx/deals/{dealId}/participants")
               ?? [];
    }

    public async Task<DealParticipantResponse?> AddParticipantAsync(string dealId, AddParticipantRequest request)
    {
        var response = await _http.PostAsJsonAsync($"tx/deals/{dealId}/participants", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DealParticipantResponse>();
        return null;
    }

    public async Task<bool> RemoveParticipantAsync(string dealId, string userId)
    {
        var response = await _http.DeleteAsync($"tx/deals/{dealId}/participants/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<DealDocumentResponse>> GetDocumentsAsync(string dealId)
    {
        return await _http.GetFromJsonAsync<List<DealDocumentResponse>>($"tx/deals/{dealId}/documents")
               ?? [];
    }

    public async Task<PresignedUrlResponse?> GetUploadUrlAsync(string dealId, string filename)
    {
        var response = await _http.PostAsync(
            $"tx/deals/{dealId}/documents?filename={Uri.EscapeDataString(filename)}", null);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PresignedUrlResponse>();
        return null;
    }

    public async Task<PresignedUrlResponse?> GetDownloadUrlAsync(string dealId, string documentId)
    {
        return await _http.GetFromJsonAsync<PresignedUrlResponse>(
            $"tx/deals/{dealId}/documents/{documentId}/url");
    }

    public async Task<List<DealAuditEntryResponse>> GetAuditLogAsync(string dealId)
    {
        return await _http.GetFromJsonAsync<List<DealAuditEntryResponse>>($"tx/deals/{dealId}/audit")
               ?? [];
    }
}

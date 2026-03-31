using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public interface IDealRoomService
{
    Task<DealRoomListResponse> GetDealRoomsAsync(string? nextToken = null);
    Task<DealRoomResponse?> GetDealRoomAsync(string dealId);
    Task<DealRoomResponse?> CreateAsync(CreateDealRoomRequest request);
    Task<List<DealParticipantResponse>> GetParticipantsAsync(string dealId);
    Task<DealParticipantResponse?> AddParticipantAsync(string dealId, AddParticipantRequest request);
    Task<bool> RemoveParticipantAsync(string dealId, string userId);
    Task<List<DealDocumentResponse>> GetDocumentsAsync(string dealId);
    Task<PresignedUrlResponse?> GetUploadUrlAsync(string dealId, string filename);
    Task<PresignedUrlResponse?> GetDownloadUrlAsync(string dealId, string documentId);
    Task<List<DealAuditEntryResponse>> GetAuditLogAsync(string dealId);
}

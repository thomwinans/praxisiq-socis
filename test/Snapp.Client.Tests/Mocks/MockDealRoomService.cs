using Snapp.Client.Services;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Tests.Mocks;

public class MockDealRoomService : IDealRoomService
{
    public DealRoomListResponse DealRoomList { get; set; } = new();
    public DealRoomResponse? DealRoom { get; set; }
    public DealRoomResponse? CreatedDealRoom { get; set; }
    public List<DealParticipantResponse> Participants { get; set; } = [];
    public DealParticipantResponse? AddedParticipant { get; set; }
    public bool RemoveResult { get; set; } = true;
    public List<DealDocumentResponse> Documents { get; set; } = [];
    public PresignedUrlResponse? UploadUrl { get; set; }
    public PresignedUrlResponse? DownloadUrl { get; set; }
    public List<DealAuditEntryResponse> AuditLog { get; set; } = [];
    public bool ShouldThrow { get; set; }

    public CreateDealRoomRequest? LastCreateRequest { get; private set; }
    public AddParticipantRequest? LastAddParticipantRequest { get; private set; }
    public string? LastUploadFilename { get; private set; }

    public Task<DealRoomListResponse> GetDealRoomsAsync(string? nextToken = null)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(DealRoomList);
    }

    public Task<DealRoomResponse?> GetDealRoomAsync(string dealId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(DealRoom);
    }

    public Task<DealRoomResponse?> CreateAsync(CreateDealRoomRequest request)
    {
        LastCreateRequest = request;
        return Task.FromResult(CreatedDealRoom);
    }

    public Task<List<DealParticipantResponse>> GetParticipantsAsync(string dealId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(Participants);
    }

    public Task<DealParticipantResponse?> AddParticipantAsync(string dealId, AddParticipantRequest request)
    {
        LastAddParticipantRequest = request;
        return Task.FromResult(AddedParticipant);
    }

    public Task<bool> RemoveParticipantAsync(string dealId, string userId)
    {
        return Task.FromResult(RemoveResult);
    }

    public Task<List<DealDocumentResponse>> GetDocumentsAsync(string dealId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(Documents);
    }

    public Task<PresignedUrlResponse?> GetUploadUrlAsync(string dealId, string filename)
    {
        LastUploadFilename = filename;
        return Task.FromResult(UploadUrl);
    }

    public Task<PresignedUrlResponse?> GetDownloadUrlAsync(string dealId, string documentId)
    {
        return Task.FromResult(DownloadUrl);
    }

    public Task<List<DealAuditEntryResponse>> GetAuditLogAsync(string dealId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(AuditLog);
    }
}

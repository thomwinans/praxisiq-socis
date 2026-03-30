using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for deal room entities in the snapp-tx DynamoDB table.
/// Handles deal rooms, participants, documents, and audit trail.
/// </summary>
public interface IDealRoomRepository
{
    /// <summary>Creates a new deal room. PK=DEAL#{dealId}, SK=META.</summary>
    Task CreateDealRoomAsync(DealRoom dealRoom);

    /// <summary>Retrieves a deal room by ID. Returns null if not found.</summary>
    Task<DealRoom?> GetDealRoomAsync(string dealId);

    /// <summary>Updates deal room metadata (name, status).</summary>
    Task UpdateDealRoomAsync(DealRoom dealRoom);

    /// <summary>Lists deal rooms where the user is a participant. Supports pagination.</summary>
    Task<List<DealRoom>> ListUserDealRoomsAsync(string userId, string? nextToken);

    /// <summary>Adds a participant to a deal room. PK=DEAL#{dealId}, SK=PART#{userId}.</summary>
    Task AddParticipantAsync(DealParticipant participant);

    /// <summary>Removes a participant from a deal room.</summary>
    Task RemoveParticipantAsync(string dealId, string userId);

    /// <summary>Lists all participants in a deal room.</summary>
    Task<List<DealParticipant>> ListParticipantsAsync(string dealId);

    /// <summary>Checks if a user is a participant in a deal room.</summary>
    Task<bool> IsParticipantAsync(string dealId, string userId);

    /// <summary>Creates a document metadata record. PK=DEAL#{dealId}, SK=DOC#{timestamp}#{docId}.</summary>
    Task CreateDocumentAsync(DealDocument document);

    /// <summary>Lists documents in a deal room, ordered by upload time descending.</summary>
    Task<List<DealDocument>> ListDocumentsAsync(string dealId, string? nextToken);

    /// <summary>Retrieves a specific document by deal and document ID. Returns null if not found.</summary>
    Task<DealDocument?> GetDocumentAsync(string dealId, string documentId);

    /// <summary>Appends an audit entry. PK=DEAL#{dealId}, SK=AUDIT#{timestamp}#{eventId}.</summary>
    Task CreateAuditEntryAsync(DealAuditEntry entry);

    /// <summary>Lists audit entries for a deal room, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<DealAuditEntry>> ListAuditEntriesAsync(string dealId, string? nextToken, int limit = 50);
}

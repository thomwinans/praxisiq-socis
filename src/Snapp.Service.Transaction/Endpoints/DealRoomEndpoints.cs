using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Transaction.Repositories;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Endpoints;

public static class DealRoomEndpoints
{
    private const string BucketName = "snapp-deals";
    private static readonly HashSet<string> ValidRoles = ["seller", "buyer", "advisor"];
    private const int PresignedUrlExpirySeconds = 900; // 15 minutes

    public static void MapDealRoomEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tx/deals", HandleCreateDeal)
            .WithName("CreateDealRoom")
            .WithTags("DealRooms")
            .Accepts<CreateDealRoomRequest>("application/json")
            .Produces<DealRoomResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/tx/deals", HandleListDeals)
            .WithName("ListDealRooms")
            .WithTags("DealRooms")
            .Produces<DealRoomListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/tx/deals/{dealId}", HandleGetDeal)
            .WithName("GetDealRoom")
            .WithTags("DealRooms")
            .Produces<DealRoomResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/tx/deals/{dealId}/participants", HandleAddParticipant)
            .WithName("AddDealParticipant")
            .WithTags("DealRooms")
            .Accepts<AddParticipantRequest>("application/json")
            .Produces<DealParticipantResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(409)
            .WithOpenApi();

        app.MapDelete("/api/tx/deals/{dealId}/participants/{userId}", HandleRemoveParticipant)
            .WithName("RemoveDealParticipant")
            .WithTags("DealRooms")
            .Produces(204)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/tx/deals/{dealId}/documents", HandleUploadDocument)
            .WithName("GenerateDocumentUploadUrl")
            .WithTags("DealRooms")
            .Produces<PresignedUrlResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/tx/deals/{dealId}/documents", HandleListDocuments)
            .WithName("ListDealDocuments")
            .WithTags("DealRooms")
            .Produces<List<DealDocumentResponse>>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/tx/deals/{dealId}/documents/{docId}/url", HandleGetDocumentUrl)
            .WithName("GetDocumentDownloadUrl")
            .WithTags("DealRooms")
            .Produces<PresignedUrlResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/tx/deals/{dealId}/audit", HandleGetAudit)
            .WithName("GetDealAuditTrail")
            .WithTags("DealRooms")
            .Produces<List<DealAuditEntryResponse>>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();
    }

    // ── Handlers ────────────────────────────────────────────────

    private static async Task<IResult> HandleCreateDeal(
        [FromBody] CreateDealRoomRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        IDealRoomRepository dealRepo,
        ILogger<CreateDealRoomRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Error(400, "INVALID_REQUEST", "Deal room name is required.");

        var dealId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        var dealRoom = new DealRoom
        {
            DealId = dealId,
            Name = request.Name.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now,
        };

        await dealRepo.CreateDealRoomAsync(dealRoom);

        // Add creator as first participant (seller by default)
        await dealRepo.AddParticipantAsync(new DealParticipant
        {
            DealId = dealId,
            UserId = userId,
            Role = "seller",
            AddedAt = now,
        });

        await AuditAsync(dealRepo, dealId, userId, "deal_created", $"Deal room '{request.Name.Trim()}' created");

        logger.LogInformation("Deal room {DealId} created by {UserId}", dealId, userId);

        var participants = await dealRepo.ListParticipantsAsync(dealId);
        return Results.Created($"/api/tx/deals/{dealId}", MapToResponse(dealRoom, participants.Count, 0));
    }

    private static async Task<IResult> HandleListDeals(
        [FromHeader(Name = "X-User-Id")] string? userId,
        [FromQuery] string? nextToken,
        IDealRoomRepository dealRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        var deals = await dealRepo.ListUserDealRoomsAsync(userId, nextToken);

        var responses = new List<DealRoomResponse>();
        foreach (var deal in deals)
        {
            var participants = await dealRepo.ListParticipantsAsync(deal.DealId);
            var documents = await dealRepo.ListDocumentsAsync(deal.DealId, null);
            responses.Add(MapToResponse(deal, participants.Count, documents.Count));
        }

        return Results.Ok(new DealRoomListResponse { DealRooms = responses });
    }

    private static async Task<IResult> HandleGetDeal(
        string dealId,
        [FromHeader(Name = "X-User-Id")] string? userId,
        IDealRoomRepository dealRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        var deal = await dealRepo.GetDealRoomAsync(dealId);
        if (deal == null)
            return Error(404, "NOT_FOUND", "Deal room not found.");

        var participants = await dealRepo.ListParticipantsAsync(dealId);
        var documents = await dealRepo.ListDocumentsAsync(dealId, null);
        return Results.Ok(MapToResponse(deal, participants.Count, documents.Count));
    }

    private static async Task<IResult> HandleAddParticipant(
        string dealId,
        [FromBody] AddParticipantRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        IDealRoomRepository dealRepo,
        ILogger<AddParticipantRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        if (string.IsNullOrEmpty(request.UserId))
            return Error(400, "INVALID_REQUEST", "UserId is required.");

        var role = request.Role?.ToLowerInvariant() ?? "";
        if (!ValidRoles.Contains(role))
            return Error(400, "INVALID_ROLE", "Role must be one of: seller, buyer, advisor.");

        if (await dealRepo.IsParticipantAsync(dealId, request.UserId))
            return Error(409, "ALREADY_PARTICIPANT", "User is already a participant.");

        var now = DateTime.UtcNow;
        var participant = new DealParticipant
        {
            DealId = dealId,
            UserId = request.UserId,
            Role = role,
            AddedAt = now,
        };

        await dealRepo.AddParticipantAsync(participant);
        await AuditAsync(dealRepo, dealId, userId, "participant_added",
            $"User {request.UserId} added as {role}");

        logger.LogInformation("Participant {ParticipantId} added to deal {DealId} as {Role} by {ActorId}",
            request.UserId, dealId, role, userId);

        return Results.Created($"/api/tx/deals/{dealId}/participants/{request.UserId}",
            new DealParticipantResponse
            {
                UserId = participant.UserId,
                DisplayName = participant.UserId, // resolved by client via user service
                Role = participant.Role,
                AddedAt = participant.AddedAt,
            });
    }

    private static async Task<IResult> HandleRemoveParticipant(
        string dealId,
        string userId,
        [FromHeader(Name = "X-User-Id")] string? actorId,
        IDealRoomRepository dealRepo,
        ILogger<DealRoom> logger)
    {
        if (string.IsNullOrEmpty(actorId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, actorId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(404, "NOT_FOUND", "Participant not found.");

        var deal = await dealRepo.GetDealRoomAsync(dealId);
        if (deal == null)
            return Error(404, "NOT_FOUND", "Deal room not found.");

        // Only the deal creator can remove others; anyone can remove themselves
        if (userId != actorId && deal.CreatedByUserId != actorId)
            return Error(403, "FORBIDDEN", "Only the deal creator can remove other participants.");

        await dealRepo.RemoveParticipantAsync(dealId, userId);
        await AuditAsync(dealRepo, dealId, actorId, "participant_removed", $"User {userId} removed");

        logger.LogInformation("Participant {ParticipantId} removed from deal {DealId} by {ActorId}",
            userId, dealId, actorId);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUploadDocument(
        string dealId,
        [FromQuery] string filename,
        [FromHeader(Name = "X-User-Id")] string? userId,
        IDealRoomRepository dealRepo,
        IAmazonS3 s3,
        ILogger<DealRoom> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        if (string.IsNullOrWhiteSpace(filename))
            return Error(400, "INVALID_REQUEST", "Filename query parameter is required.");

        var docId = Ulid.NewUlid().ToString();
        var s3Key = $"deals/{dealId}/{docId}/{filename.Trim()}";
        var now = DateTime.UtcNow;

        // Generate pre-signed upload URL
        var presignedRequest = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = s3Key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddSeconds(PresignedUrlExpirySeconds),
        };
        var uploadUrl = await s3.GetPreSignedURLAsync(presignedRequest);

        // Store document metadata
        await dealRepo.CreateDocumentAsync(new DealDocument
        {
            DocumentId = docId,
            DealId = dealId,
            Filename = filename.Trim(),
            S3Key = s3Key,
            UploadedByUserId = userId,
            CreatedAt = now,
        });

        await AuditAsync(dealRepo, dealId, userId, "document_uploaded", $"Document '{filename.Trim()}' uploaded");

        logger.LogInformation("Document {DocId} upload URL generated for deal {DealId} by {UserId}",
            docId, dealId, userId);

        return Results.Ok(new PresignedUrlResponse
        {
            Url = uploadUrl,
            ExpiresIn = PresignedUrlExpirySeconds,
        });
    }

    private static async Task<IResult> HandleListDocuments(
        string dealId,
        [FromHeader(Name = "X-User-Id")] string? userId,
        [FromQuery] string? nextToken,
        IDealRoomRepository dealRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        var documents = await dealRepo.ListDocumentsAsync(dealId, nextToken);
        var responses = documents.Select(d => new DealDocumentResponse
        {
            DocumentId = d.DocumentId,
            Filename = d.Filename,
            UploadedByUserId = d.UploadedByUserId,
            UploadedByDisplayName = d.UploadedByUserId,
            Size = d.Size,
            CreatedAt = d.CreatedAt,
        }).ToList();

        return Results.Ok(responses);
    }

    private static async Task<IResult> HandleGetDocumentUrl(
        string dealId,
        string docId,
        [FromHeader(Name = "X-User-Id")] string? userId,
        IDealRoomRepository dealRepo,
        IAmazonS3 s3)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        var document = await dealRepo.GetDocumentAsync(dealId, docId);
        if (document == null)
            return Error(404, "NOT_FOUND", "Document not found.");

        var presignedRequest = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = document.S3Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddSeconds(PresignedUrlExpirySeconds),
        };
        var downloadUrl = await s3.GetPreSignedURLAsync(presignedRequest);

        return Results.Ok(new PresignedUrlResponse
        {
            Url = downloadUrl,
            ExpiresIn = PresignedUrlExpirySeconds,
        });
    }

    private static async Task<IResult> HandleGetAudit(
        string dealId,
        [FromHeader(Name = "X-User-Id")] string? userId,
        [FromQuery] string? nextToken,
        IDealRoomRepository dealRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (!await dealRepo.IsParticipantAsync(dealId, userId))
            return Error(403, "NOT_PARTICIPANT", "You are not a participant in this deal room.");

        var entries = await dealRepo.ListAuditEntriesAsync(dealId, nextToken);
        var responses = entries.Select(e => new DealAuditEntryResponse
        {
            EventId = e.EventId,
            Action = e.Action,
            ActorUserId = e.ActorUserId,
            ActorDisplayName = e.ActorUserId,
            Details = e.Details,
            CreatedAt = e.CreatedAt,
        }).ToList();

        return Results.Ok(responses);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static async Task AuditAsync(IDealRoomRepository repo, string dealId, string actorUserId, string action, string? details)
    {
        await repo.CreateAuditEntryAsync(new DealAuditEntry
        {
            EventId = Ulid.NewUlid().ToString(),
            DealId = dealId,
            Action = action,
            ActorUserId = actorUserId,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static DealRoomResponse MapToResponse(DealRoom deal, int participantCount, int documentCount) => new()
    {
        DealId = deal.DealId,
        Name = deal.Name,
        CreatedByUserId = deal.CreatedByUserId,
        Status = deal.Status,
        ParticipantCount = participantCount,
        DocumentCount = documentCount,
        CreatedAt = deal.CreatedAt,
    };

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message }
        }, statusCode: status);
}

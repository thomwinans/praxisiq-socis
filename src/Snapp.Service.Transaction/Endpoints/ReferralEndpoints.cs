using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Transaction.Repositories;
using Snapp.Service.Transaction.Services;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Endpoints;

public static class ReferralEndpoints
{
    public static void MapReferralEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tx/referrals", HandleCreateReferral)
            .WithName("CreateReferral")
            .WithTags("Referrals")
            .Accepts<CreateReferralRequest>("application/json")
            .Produces<ReferralResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/tx/referrals/sent", HandleListSent)
            .WithName("ListSentReferrals")
            .WithTags("Referrals")
            .Produces<ReferralListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/tx/referrals/received", HandleListReceived)
            .WithName("ListReceivedReferrals")
            .WithTags("Referrals")
            .Produces<ReferralListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPut("/api/tx/referrals/{refId}/status", HandleUpdateStatus)
            .WithName("UpdateReferralStatus")
            .WithTags("Referrals")
            .Accepts<UpdateReferralStatusRequest>("application/json")
            .Produces<ReferralResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/tx/referrals/{refId}/outcome", HandleRecordOutcome)
            .WithName("RecordReferralOutcome")
            .WithTags("Referrals")
            .Accepts<RecordOutcomeRequest>("application/json")
            .Produces<ReferralResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleCreateReferral(
        [FromBody] CreateReferralRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        TransactionRepository txRepo,
        INetworkRepository networkRepo,
        ILogger<CreateReferralRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (string.IsNullOrEmpty(request.ReceiverUserId))
            return Error(400, "INVALID_REQUEST", "ReceiverUserId is required.");

        if (userId == request.ReceiverUserId)
            return Error(400, "SELF_REFERRAL", "Cannot refer yourself.");

        // Verify both users are members of the network
        var senderMembership = await networkRepo.GetMembershipAsync(request.NetworkId, userId);
        if (senderMembership == null)
            return Error(403, "NOT_MEMBER", "Sender is not a member of this network.");

        var receiverMembership = await networkRepo.GetMembershipAsync(request.NetworkId, request.ReceiverUserId);
        if (receiverMembership == null)
            return Error(403, "RECEIVER_NOT_MEMBER", "Receiver is not a member of this network.");

        var referral = new Referral
        {
            ReferralId = Ulid.NewUlid().ToString(),
            SenderUserId = userId,
            ReceiverUserId = request.ReceiverUserId,
            NetworkId = request.NetworkId,
            Specialty = request.Specialty,
            Notes = request.Notes,
            Status = ReferralStatus.Created,
            CreatedAt = DateTime.UtcNow,
        };

        await txRepo.CreateReferralAsync(referral);

        logger.LogInformation("Referral {ReferralId} created by {SenderId} for {ReceiverId} in network {NetworkId}",
            referral.ReferralId, userId, request.ReceiverUserId, request.NetworkId);

        return Results.Created($"/api/tx/referrals/{referral.ReferralId}", MapToResponse(referral));
    }

    private static async Task<IResult> HandleListSent(
        [FromHeader(Name = "X-User-Id")] string? userId,
        [FromQuery] string? nextToken,
        TransactionRepository txRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        var referrals = await txRepo.ListSentReferralsAsync(userId, nextToken);
        return Results.Ok(new ReferralListResponse
        {
            Referrals = referrals.Select(MapToResponse).ToList(),
        });
    }

    private static async Task<IResult> HandleListReceived(
        [FromHeader(Name = "X-User-Id")] string? userId,
        [FromQuery] string? nextToken,
        TransactionRepository txRepo)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        var referrals = await txRepo.ListReceivedReferralsAsync(userId, nextToken);
        return Results.Ok(new ReferralListResponse
        {
            Referrals = referrals.Select(MapToResponse).ToList(),
        });
    }

    private static async Task<IResult> HandleUpdateStatus(
        string refId,
        [FromBody] UpdateReferralStatusRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        TransactionRepository txRepo,
        ILogger<UpdateReferralStatusRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        var referral = await txRepo.GetReferralAsync(refId);
        if (referral == null)
            return Error(404, "NOT_FOUND", "Referral not found.");

        // Only receiver can accept; sender or receiver can expire
        if (request.Status == ReferralStatus.Accepted && userId != referral.ReceiverUserId)
            return Error(403, "FORBIDDEN", "Only the receiver can accept a referral.");

        // Validate status transitions
        if (!IsValidTransition(referral.Status, request.Status))
            return Error(400, "INVALID_TRANSITION",
                $"Cannot transition from {referral.Status} to {request.Status}.");

        referral.Status = request.Status;
        await txRepo.UpdateReferralAsync(referral);

        logger.LogInformation("Referral {ReferralId} status updated to {Status} by {UserId}",
            refId, request.Status, userId);

        return Results.Ok(MapToResponse(referral));
    }

    private static async Task<IResult> HandleRecordOutcome(
        string refId,
        [FromBody] RecordOutcomeRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        TransactionRepository txRepo,
        ReputationComputeHandler reputationHandler,
        ILogger<RecordOutcomeRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        var referral = await txRepo.GetReferralAsync(refId);
        if (referral == null)
            return Error(404, "NOT_FOUND", "Referral not found.");

        if (referral.Status != ReferralStatus.Accepted)
            return Error(400, "INVALID_STATE", "Referral must be in Accepted state to record an outcome.");

        if (userId != referral.SenderUserId && userId != referral.ReceiverUserId)
            return Error(403, "FORBIDDEN", "Only referral participants can record outcomes.");

        referral.Status = request.Success ? ReferralStatus.Completed : ReferralStatus.Expired;
        referral.OutcomeRecordedAt = DateTime.UtcNow;
        referral.Notes = string.IsNullOrEmpty(referral.Notes)
            ? request.Outcome
            : $"{referral.Notes}\n---\nOutcome: {request.Outcome}";

        await txRepo.UpdateReferralAsync(referral);

        // Trigger reputation recomputation for both parties
        _ = Task.Run(async () =>
        {
            try
            {
                await reputationHandler.ComputeAsync(referral.SenderUserId);
                await reputationHandler.ComputeAsync(referral.ReceiverUserId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recompute reputation after outcome for {ReferralId}", refId);
            }
        });

        logger.LogInformation("Outcome recorded for referral {ReferralId}: success={Success} by {UserId}",
            refId, request.Success, userId);

        return Results.Ok(MapToResponse(referral));
    }

    private static bool IsValidTransition(ReferralStatus from, ReferralStatus to) => (from, to) switch
    {
        (ReferralStatus.Created, ReferralStatus.Accepted) => true,
        (ReferralStatus.Accepted, ReferralStatus.Completed) => true,
        (_, ReferralStatus.Expired) => true,
        _ => false,
    };

    private static ReferralResponse MapToResponse(Referral r) => new()
    {
        ReferralId = r.ReferralId,
        SenderUserId = r.SenderUserId,
        ReceiverUserId = r.ReceiverUserId,
        NetworkId = r.NetworkId,
        Specialty = r.Specialty,
        Status = r.Status,
        Notes = r.Notes,
        CreatedAt = r.CreatedAt,
        OutcomeRecordedAt = r.OutcomeRecordedAt,
    };

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message }
        }, statusCode: status);
}

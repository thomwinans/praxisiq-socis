using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Transaction.Models;
using Snapp.Service.Transaction.Repositories;
using Snapp.Service.Transaction.Services;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Interfaces;
using CreateAttestationRequest = Snapp.Service.Transaction.DTOs.CreateAttestationRequest;

namespace Snapp.Service.Transaction.Endpoints;

public static class ReputationEndpoints
{
    public static void MapReputationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tx/reputation/{userId}", HandleGetReputation)
            .WithName("GetReputation")
            .WithTags("Reputation")
            .Produces<ReputationResponse>(200)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/tx/reputation/{userId}/history", HandleGetHistory)
            .WithName("GetReputationHistory")
            .WithTags("Reputation")
            .Produces<ReputationHistoryResponse>(200)
            .WithOpenApi();

        app.MapPost("/api/tx/attestations", HandleCreateAttestation)
            .WithName("CreateAttestation")
            .WithTags("Attestations")
            .Accepts<CreateAttestationRequest>("application/json")
            .Produces<AttestationResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(409)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetReputation(
        string userId,
        TransactionRepository txRepo)
    {
        var reputation = await txRepo.GetReputationAsync(userId);
        if (reputation == null)
        {
            return Results.Ok(new ReputationResponse
            {
                UserId = userId,
                OverallScore = 0,
                ReferralScore = 0,
                ContributionScore = 0,
                AttestationScore = 0,
                ComputedAt = DateTime.UtcNow,
            });
        }

        return Results.Ok(new ReputationResponse
        {
            UserId = reputation.UserId,
            OverallScore = reputation.OverallScore,
            ReferralScore = reputation.ReferralScore,
            ContributionScore = reputation.ContributionScore,
            AttestationScore = reputation.AttestationScore,
            ComputedAt = reputation.ComputedAt,
        });
    }

    private static async Task<IResult> HandleGetHistory(
        string userId,
        [FromQuery] string? nextToken,
        TransactionRepository txRepo)
    {
        var history = await txRepo.ListReputationHistoryAsync(userId, nextToken);

        return Results.Ok(new ReputationHistoryResponse
        {
            Points = history.Select(r => new ReputationHistoryPoint
            {
                Date = r.ComputedAt,
                OverallScore = r.OverallScore,
            }).ToList(),
        });
    }

    private static async Task<IResult> HandleCreateAttestation(
        [FromBody] CreateAttestationRequest request,
        [FromHeader(Name = "X-User-Id")] string? userId,
        TransactionRepository txRepo,
        INetworkRepository networkRepo,
        ReputationComputeHandler reputationHandler,
        ILogger<CreateAttestationRequest> logger)
    {
        if (string.IsNullOrEmpty(userId))
            return Error(401, "UNAUTHORIZED", "Missing user identity.");

        if (userId == request.TargetUserId)
            return Error(400, "SELF_ATTESTATION", "Cannot attest yourself.");

        if (string.IsNullOrEmpty(request.TargetUserId))
            return Error(400, "INVALID_REQUEST", "TargetUserId is required.");

        // Check that attestor and target share at least one network
        var attestorNetworks = await networkRepo.ListUserNetworksAsync(userId, null);
        var targetNetworks = await networkRepo.ListUserNetworksAsync(request.TargetUserId, null);
        var sharedNetworks = attestorNetworks
            .Select(n => n.NetworkId)
            .Intersect(targetNetworks.Select(n => n.NetworkId))
            .ToList();

        if (sharedNetworks.Count == 0)
            return Error(403, "NO_SHARED_NETWORK", "You must share a network with the target user to attest them.");

        // Check for duplicate attestation
        var existing = await txRepo.GetAttestationAsync(request.TargetUserId, userId);
        if (existing != null)
            return Error(409, "DUPLICATE", "You have already attested this user.");

        // Anti-gaming check (flag but don't reject)
        var antiGaming = await reputationHandler.DetectAntiGamingAsync(userId, request.TargetUserId);
        if (antiGaming.Flagged)
        {
            logger.LogWarning("Anti-gaming flag on attestation {Attestor} -> {Target}: {Reason}",
                userId, request.TargetUserId, antiGaming.Reason);
        }

        var attestation = new Attestation
        {
            TargetUserId = request.TargetUserId,
            AttestorUserId = userId,
            Skill = request.Skill,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
        };

        await txRepo.CreateAttestationAsync(attestation);

        // Trigger reputation recomputation for the target
        _ = Task.Run(async () =>
        {
            try
            {
                await reputationHandler.ComputeAsync(request.TargetUserId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recompute reputation after attestation for {UserId}", request.TargetUserId);
            }
        });

        logger.LogInformation("Attestation created: {Attestor} -> {Target} for skill {Skill}{Flag}",
            userId, request.TargetUserId, request.Skill, antiGaming.Flagged ? " [FLAGGED]" : "");

        return Results.Created("/api/tx/attestations", new Snapp.Shared.DTOs.Transaction.AttestationResponse
        {
            AttestationId = $"{attestation.TargetUserId}#{attestation.AttestorUserId}",
            FromUserId = attestation.AttestorUserId,
            FromDisplayName = attestation.AttestorUserId,
            ToUserId = attestation.TargetUserId,
            Text = $"{attestation.Skill}: {attestation.Comment ?? ""}".TrimEnd(' ', ':'),
            CreatedAt = attestation.CreatedAt,
        });
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message }
        }, statusCode: status);
}

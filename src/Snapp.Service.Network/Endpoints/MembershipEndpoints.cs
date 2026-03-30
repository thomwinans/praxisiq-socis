using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Network.Repositories;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Network;
using Snapp.Shared.Enums;

using static Snapp.Service.Network.Endpoints.NetworkEndpoints;

namespace Snapp.Service.Network.Endpoints;

public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this WebApplication app)
    {
        app.MapPost("/api/networks/{netId}/apply", HandleApply)
            .WithName("ApplyToNetwork")
            .WithTags("Membership")
            .Accepts<ApplyRequest>("application/json")
            .Produces<MessageResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/networks/{netId}/applications", HandleListApplications)
            .WithName("ListApplications")
            .WithTags("Membership")
            .Produces<List<ApplicationResponse>>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapPost("/api/networks/{netId}/applications/{userId}/decide", HandleDecide)
            .WithName("DecideApplication")
            .WithTags("Membership")
            .Accepts<ApplicationDecisionRequest>("application/json")
            .Produces<MessageResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/networks/{netId}/invite", HandleInvite)
            .WithName("InviteMember")
            .WithTags("Membership")
            .Accepts<InviteRequest>("application/json")
            .Produces<MessageResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleApply(
        string netId,
        [FromBody] ApplyRequest body,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var network = await repo.GetByIdAsync(netId);
        if (network is null)
            return NotFound(traceId, ErrorCodes.NetworkNotFound, "Network not found.");

        // Check if already a member
        var existing = await repo.GetMembershipAsync(netId, userId);
        if (existing is not null)
            return BadRequest(traceId, ErrorCodes.AlreadyAMember, "You are already a member of this network.");

        // Check if application already exists
        var existingApp = await repo.GetApplicationAsync(netId, userId);
        if (existingApp is not null)
            return BadRequest(traceId, ErrorCodes.ApplicationAlreadyExists, "You already have a pending application.");

        await repo.CreateApplicationAsync(netId, userId, body.ApplicationText);

        logger.LogInformation("Application submitted networkId={NetworkId}, userId={UserId}, traceId={TraceId}",
            netId, userId, traceId);

        return Results.Created($"/api/networks/{netId}/applications", new MessageResponse
        {
            Message = "Application submitted successfully.",
        });
    }

    private static async Task<IResult> HandleListApplications(
        string netId,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        // Must have ReviewApplications permission
        var membership = await repo.GetMembershipAsync(netId, userId);
        if (membership is null)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You are not a member of this network.");

        var role = await repo.GetRoleAsync(netId, membership.Role);
        if (role is null || !role.Permissions.HasFlag(Permission.ReviewApplications))
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to review applications.");

        var applications = await repo.ListPendingApplicationsAsync(netId);

        var responses = applications.Select(app => new ApplicationResponse
        {
            UserId = app["UserId"].S,
            ApplicationText = app.TryGetValue("ApplicationText", out var text) ? text.S : null,
            Status = app["Status"].S,
            CreatedAt = DateTime.Parse(app["CreatedAt"].S),
        }).ToList();

        return Results.Ok(responses);
    }

    private static async Task<IResult> HandleDecide(
        string netId,
        string userId,
        [FromBody] ApplicationDecisionRequest body,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var callerId = ExtractUserId(request);
        if (callerId is null)
            return Unauthorized(traceId);

        // Must have ReviewApplications permission
        var callerMembership = await repo.GetMembershipAsync(netId, callerId);
        if (callerMembership is null)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You are not a member of this network.");

        var callerRole = await repo.GetRoleAsync(netId, callerMembership.Role);
        if (callerRole is null || !callerRole.Permissions.HasFlag(Permission.ReviewApplications))
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to decide applications.");

        // Get the application
        var application = await repo.GetApplicationAsync(netId, userId);
        if (application is null)
            return NotFound(traceId, ErrorCodes.NetworkNotFound, "Application not found.");

        if (application["Status"].S != "Pending")
            return BadRequest(traceId, ErrorCodes.InvalidApplicationDecision, "Application has already been decided.");

        if (body.Decision != "Approved" && body.Decision != "Denied")
            return BadRequest(traceId, ErrorCodes.InvalidApplicationDecision, "Decision must be 'Approved' or 'Denied'.");

        await repo.UpdateApplicationStatusAsync(netId, userId, body.Decision, body.Reason);

        if (body.Decision == "Approved")
        {
            var now = DateTime.UtcNow;
            await repo.AddMemberAsync(new Shared.Models.NetworkMembership
            {
                NetworkId = netId,
                UserId = userId,
                Role = "member",
                Status = MembershipStatus.Active,
                JoinedAt = now,
                ContributionScore = 0,
            });

            await repo.IncrementMemberCountAsync(netId);

            logger.LogInformation("Application approved networkId={NetworkId}, userId={UserId}, approvedBy={CallerId}, traceId={TraceId}",
                netId, userId, callerId, traceId);
        }
        else
        {
            logger.LogInformation("Application denied networkId={NetworkId}, userId={UserId}, deniedBy={CallerId}, traceId={TraceId}",
                netId, userId, callerId, traceId);
        }

        return Results.Ok(new MessageResponse
        {
            Message = $"Application {body.Decision.ToLowerInvariant()}.",
        });
    }

    private static async Task<IResult> HandleInvite(
        string netId,
        [FromBody] InviteRequest body,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var callerId = ExtractUserId(request);
        if (callerId is null)
            return Unauthorized(traceId);

        // Must be steward (ManageMembers permission)
        var callerMembership = await repo.GetMembershipAsync(netId, callerId);
        if (callerMembership is null)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You are not a member of this network.");

        var callerRole = await repo.GetRoleAsync(netId, callerMembership.Role);
        if (callerRole is null || !callerRole.Permissions.HasFlag(Permission.ManageMembers))
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to invite members.");

        if (string.IsNullOrWhiteSpace(body.UserId))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "UserId is required.");

        // Check if already a member
        var existing = await repo.GetMembershipAsync(netId, body.UserId);
        if (existing is not null)
            return BadRequest(traceId, ErrorCodes.AlreadyAMember, "User is already a member of this network.");

        var network = await repo.GetByIdAsync(netId);
        if (network is null)
            return NotFound(traceId, ErrorCodes.NetworkNotFound, "Network not found.");

        var role = body.Role ?? "member";
        var now = DateTime.UtcNow;

        await repo.AddMemberAsync(new Shared.Models.NetworkMembership
        {
            NetworkId = netId,
            UserId = body.UserId,
            Role = role,
            Status = MembershipStatus.Active,
            JoinedAt = now,
            ContributionScore = 0,
        });

        await repo.IncrementMemberCountAsync(netId);

        logger.LogInformation("Member invited networkId={NetworkId}, userId={UserId}, role={Role}, invitedBy={CallerId}, traceId={TraceId}",
            netId, body.UserId, role, callerId, traceId);

        return Results.Created($"/api/networks/{netId}/members", new MessageResponse
        {
            Message = "Member invited successfully.",
        });
    }
}

public class ApplicationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string? ApplicationText { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class InviteRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
}

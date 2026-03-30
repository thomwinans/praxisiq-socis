using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Network.Repositories;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Network;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;

namespace Snapp.Service.Network.Endpoints;

public static class NetworkEndpoints
{
    public static void MapNetworkEndpoints(this WebApplication app)
    {
        app.MapPost("/api/networks", HandleCreate)
            .WithName("CreateNetwork")
            .WithTags("Networks")
            .Accepts<CreateNetworkRequest>("application/json")
            .Produces<NetworkResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/networks", HandleList)
            .WithName("ListNetworks")
            .WithTags("Networks")
            .Produces<NetworkListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/networks/mine", HandleListMine)
            .WithName("ListMyNetworks")
            .WithTags("Networks")
            .Produces<NetworkListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/networks/{netId}", HandleGet)
            .WithName("GetNetwork")
            .WithTags("Networks")
            .Produces<NetworkResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPut("/api/networks/{netId}", HandleUpdate)
            .WithName("UpdateNetwork")
            .WithTags("Networks")
            .Accepts<UpdateNetworkRequest>("application/json")
            .Produces<NetworkResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/networks/{netId}/members", HandleListMembers)
            .WithName("ListMembers")
            .WithTags("Networks")
            .Produces<MemberListResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleCreate(
        [FromBody] CreateNetworkRequest body,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "Network name is required.");

        var networkId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        var network = new Shared.Models.Network
        {
            NetworkId = networkId,
            Name = body.Name.Trim(),
            Description = body.Description?.Trim(),
            Charter = body.Charter?.Trim(),
            CreatedByUserId = userId,
            MemberCount = 1,
            CreatedAt = now,
        };

        await repo.CreateAsync(network);

        // Create default roles
        await repo.CreateRoleAsync(networkId, new Shared.Models.NetworkRole
        {
            RoleName = "steward",
            Permissions = Permission.Admin,
            Description = "Full network administration",
        });

        await repo.CreateRoleAsync(networkId, new Shared.Models.NetworkRole
        {
            RoleName = "member",
            Permissions = Permission.ViewMembers | Permission.CreatePost | Permission.ViewIntelligence,
            Description = "Standard network member",
        });

        await repo.CreateRoleAsync(networkId, new Shared.Models.NetworkRole
        {
            RoleName = "associate",
            Permissions = Permission.ViewMembers | Permission.CreatePost,
            Description = "Limited access member",
        });

        // Add creator as steward
        await repo.AddMemberAsync(new Shared.Models.NetworkMembership
        {
            NetworkId = networkId,
            UserId = userId,
            Role = "steward",
            Status = MembershipStatus.Active,
            JoinedAt = now,
            ContributionScore = 0,
        });

        logger.LogInformation("Network created networkId={NetworkId}, createdBy={UserId}, traceId={TraceId}",
            networkId, userId, traceId);

        return Results.Created($"/api/networks/{networkId}", MapToResponse(network, "steward"));
    }

    private static async Task<IResult> HandleList(
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var networks = await repo.ListAsync(null);

        var responses = new List<NetworkResponse>();
        foreach (var net in networks)
        {
            var membership = await repo.GetMembershipAsync(net.NetworkId, userId);
            responses.Add(MapToResponse(net, membership?.Role));
        }

        return Results.Ok(new NetworkListResponse { Networks = responses });
    }

    private static async Task<IResult> HandleListMine(
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var networks = await repo.ListUserNetworksAsync(userId, null);

        var responses = new List<NetworkResponse>();
        foreach (var net in networks)
        {
            var membership = await repo.GetMembershipAsync(net.NetworkId, userId);
            responses.Add(MapToResponse(net, membership?.Role));
        }

        return Results.Ok(new NetworkListResponse { Networks = responses });
    }

    private static async Task<IResult> HandleGet(
        string netId,
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

        var membership = await repo.GetMembershipAsync(netId, userId);
        return Results.Ok(MapToResponse(network, membership?.Role));
    }

    private static async Task<IResult> HandleUpdate(
        string netId,
        [FromBody] UpdateNetworkRequest body,
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

        // Only stewards can update
        var membership = await repo.GetMembershipAsync(netId, userId);
        if (membership is null)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You are not a member of this network.");

        var role = await repo.GetRoleAsync(netId, membership.Role);
        if (role is null || !role.Permissions.HasFlag(Permission.ManageNetwork))
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to update this network.");

        if (body.Name is not null) network.Name = body.Name.Trim();
        if (body.Description is not null) network.Description = body.Description.Trim();
        if (body.Charter is not null) network.Charter = body.Charter.Trim();

        await repo.UpdateAsync(network);

        logger.LogInformation("Network updated networkId={NetworkId}, updatedBy={UserId}, traceId={TraceId}",
            netId, userId, traceId);

        return Results.Ok(MapToResponse(network, membership.Role));
    }

    private static async Task<IResult> HandleListMembers(
        string netId,
        HttpRequest request,
        NetworkRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        // Must be a member to see member list
        var membership = await repo.GetMembershipAsync(netId, userId);
        if (membership is null)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member to view the member list.");

        var members = await repo.ListMembersAsync(netId, null);

        var responses = members.Select(m => new MemberResponse
        {
            UserId = m.UserId,
            DisplayName = m.UserId, // Display name would come from user service; use userId as fallback
            Role = m.Role,
            JoinedAt = m.JoinedAt,
            ContributionScore = m.ContributionScore,
        }).ToList();

        return Results.Ok(new MemberListResponse { Members = responses });
    }

    // ── Helpers ───────────────────────────────────────────────────

    internal static string? ExtractUserId(HttpRequest request)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..];
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return jwt.Subject;
        }
        catch
        {
            return null;
        }
    }

    internal static NetworkResponse MapToResponse(Shared.Models.Network network, string? userRole) => new()
    {
        NetworkId = network.NetworkId,
        Name = network.Name,
        Description = network.Description,
        Charter = network.Charter,
        MemberCount = network.MemberCount,
        CreatedAt = network.CreatedAt,
        UserRole = userRole,
    };

    internal static IResult Unauthorized(string traceId) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = ErrorCodes.Unauthorized,
                Message = "Authentication required.",
                TraceId = traceId,
            },
        }, statusCode: 401);

    internal static IResult NotFound(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                TraceId = traceId,
            },
        }, statusCode: 404);

    internal static IResult Forbidden(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                TraceId = traceId,
            },
        }, statusCode: 403);

    internal static IResult BadRequest(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                TraceId = traceId,
            },
        }, statusCode: 400);
}

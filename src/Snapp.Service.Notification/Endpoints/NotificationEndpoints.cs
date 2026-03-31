using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Notification.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Notification.Endpoints;

public static partial class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notif", HandleListNotifications)
            .WithName("ListNotifications")
            .WithTags("Notifications")
            .Produces<NotificationListResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/notif/{notifId}/read", HandleMarkRead)
            .WithName("MarkNotificationRead")
            .WithTags("Notifications")
            .Produces(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/notif/read-all", HandleMarkAllRead)
            .WithName("MarkAllNotificationsRead")
            .WithTags("Notifications")
            .Produces(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/notif/preferences", HandleGetPreferences)
            .WithName("GetNotificationPreferences")
            .WithTags("Notifications")
            .Produces<NotificationPreferences>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPut("/api/notif/preferences", HandleSavePreferences)
            .WithName("SaveNotificationPreferences")
            .WithTags("Notifications")
            .Accepts<UpdatePreferencesRequest>("application/json")
            .Produces(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleListNotifications(
        HttpRequest request,
        NotificationRepository repo,
        [FromQuery] string? nextToken = null,
        [FromQuery] int limit = 25)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var notifications = await repo.ListUserNotificationsAsync(userId, nextToken, limit);
        var unreadCount = await repo.CountUnreadAsync(userId);

        return Results.Ok(new NotificationListResponse
        {
            Notifications = notifications.Select(n => new NotificationResponse
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                Category = n.Category,
                Title = n.Title,
                Body = n.Body,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            }).ToList(),
            UnreadCount = unreadCount,
        });
    }

    private static async Task<IResult> HandleMarkRead(
        string notifId,
        HttpRequest request,
        NotificationRepository repo)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        await repo.MarkReadAsync(userId, notifId);
        return Results.Ok(new { Message = "Notification marked as read." });
    }

    private static async Task<IResult> HandleMarkAllRead(
        HttpRequest request,
        NotificationRepository repo)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        await repo.MarkAllReadAsync(userId);
        return Results.Ok(new { Message = "All notifications marked as read." });
    }

    private static async Task<IResult> HandleGetPreferences(
        HttpRequest request,
        NotificationRepository repo)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var prefs = await repo.GetPreferencesAsync(userId);
        if (prefs is null)
        {
            // Return defaults
            return Results.Ok(new NotificationPreferences
            {
                UserId = userId,
                DigestTime = "07:00",
                Timezone = "America/New_York",
                ImmediateTypes = [],
            });
        }

        return Results.Ok(prefs);
    }

    private static async Task<IResult> HandleSavePreferences(
        [FromBody] UpdatePreferencesRequest body,
        HttpRequest request,
        NotificationRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        // Validate DigestTime format
        if (body.DigestTime is not null && !HhMmRegex().IsMatch(body.DigestTime))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "DigestTime must be in HH:mm format (24-hour).");

        // Validate Timezone is a valid IANA timezone
        if (body.Timezone is not null)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(body.Timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                return BadRequest(traceId, ErrorCodes.ValidationFailed, "Timezone must be a valid IANA timezone identifier.");
            }
        }

        // Merge with existing or defaults
        var existing = await repo.GetPreferencesAsync(userId);
        var prefs = new NotificationPreferences
        {
            UserId = userId,
            DigestTime = body.DigestTime ?? existing?.DigestTime ?? "07:00",
            Timezone = body.Timezone ?? existing?.Timezone ?? "America/New_York",
            ImmediateTypes = body.ImmediateTypes ?? existing?.ImmediateTypes ?? [],
        };

        await repo.SavePreferencesAsync(prefs);

        logger.LogInformation("Preferences saved userId={UserId}, traceId={TraceId}", userId, traceId);

        return Results.Ok(new { Message = "Preferences saved." });
    }

    // ── Helpers ──────────────────────────────────────────────────

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
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 404);

    internal static IResult BadRequest(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 400);

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
    private static partial Regex HhMmRegex();
}

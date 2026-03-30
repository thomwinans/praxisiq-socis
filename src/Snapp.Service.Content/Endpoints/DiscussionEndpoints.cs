using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Content.Repositories;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.Models;

namespace Snapp.Service.Content.Endpoints;

public static partial class DiscussionEndpoints
{
    public static void MapDiscussionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/content/networks/{netId}/threads", HandleCreateThread)
            .WithName("CreateThread")
            .WithTags("Discussions")
            .Accepts<CreateThreadRequest>("application/json")
            .Produces<ThreadResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/content/networks/{netId}/threads", HandleListThreads)
            .WithName("ListThreads")
            .WithTags("Discussions")
            .Produces<ThreadListResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/content/threads/{threadId}", HandleGetThread)
            .WithName("GetThread")
            .WithTags("Discussions")
            .Produces<ThreadResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPost("/api/content/threads/{threadId}/replies", HandleCreateReply)
            .WithName("CreateReply")
            .WithTags("Discussions")
            .Accepts<CreateReplyRequest>("application/json")
            .Produces<ReplyResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/content/threads/{threadId}/replies", HandleListReplies)
            .WithName("ListReplies")
            .WithTags("Discussions")
            .Produces<ReplyListResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapDelete("/api/content/threads/{threadId}/replies/{replyId}", HandleDeleteReply)
            .WithName("DeleteReply")
            .WithTags("Discussions")
            .Produces(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleCreateThread(
        string netId,
        [FromBody] CreateThreadRequest body,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "Title is required.");
        if (string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "Content is required.");

        var (isMember, role) = await repo.CheckMembershipAsync(netId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var permissions = await repo.GetRolePermissionsAsync(netId, role!);
        if (!permissions.HasFlag(Permission.CreatePost))
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to create posts.");

        var threadId = Ulid.NewUlid().ToString();
        var replyId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        var thread = new DiscussionThread
        {
            ThreadId = threadId,
            NetworkId = netId,
            Title = body.Title.Trim(),
            AuthorUserId = userId,
            ReplyCount = 1,
            LastReplyAt = now,
            CreatedAt = now,
        };

        await repo.CreateThreadAsync(thread);

        // Create the initial reply (the thread body)
        var initialReply = new Reply
        {
            ReplyId = replyId,
            ThreadId = threadId,
            AuthorUserId = userId,
            Content = body.Content.Trim(),
            CreatedAt = now,
        };
        await repo.CreateReplyAsync(initialReply);

        logger.LogInformation("Thread created threadId={ThreadId}, networkId={NetworkId}, userId={UserId}, traceId={TraceId}",
            threadId, netId, userId, traceId);

        return Results.Created($"/api/content/threads/{threadId}", new ThreadResponse
        {
            ThreadId = threadId,
            NetworkId = netId,
            Title = thread.Title,
            AuthorUserId = userId,
            AuthorDisplayName = userId,
            ReplyCount = thread.ReplyCount,
            LastReplyAt = now,
            CreatedAt = now,
        });
    }

    private static async Task<IResult> HandleListThreads(
        string netId,
        HttpRequest request,
        ContentRepository repo,
        [FromQuery] string? nextToken = null,
        [FromQuery] int limit = 25)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var (isMember, _) = await repo.CheckMembershipAsync(netId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var threads = await repo.ListThreadsAsync(netId, nextToken, limit);

        return Results.Ok(new ThreadListResponse
        {
            Threads = threads.Select(t => new ThreadResponse
            {
                ThreadId = t.ThreadId,
                NetworkId = t.NetworkId,
                Title = t.Title,
                AuthorUserId = t.AuthorUserId,
                AuthorDisplayName = t.AuthorUserId,
                ReplyCount = t.ReplyCount,
                LastReplyAt = t.LastReplyAt,
                CreatedAt = t.CreatedAt,
            }).ToList(),
        });
    }

    private static async Task<IResult> HandleGetThread(
        string threadId,
        HttpRequest request,
        ContentRepository repo)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var thread = await repo.GetThreadByIdAsync(threadId);
        if (thread is null)
            return NotFound(traceId, ErrorCodes.ThreadNotFound, "Thread not found.");

        var (isMember, _) = await repo.CheckMembershipAsync(thread.NetworkId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        return Results.Ok(new ThreadResponse
        {
            ThreadId = thread.ThreadId,
            NetworkId = thread.NetworkId,
            Title = thread.Title,
            AuthorUserId = thread.AuthorUserId,
            AuthorDisplayName = thread.AuthorUserId,
            ReplyCount = thread.ReplyCount,
            LastReplyAt = thread.LastReplyAt,
            CreatedAt = thread.CreatedAt,
        });
    }

    private static async Task<IResult> HandleCreateReply(
        string threadId,
        [FromBody] CreateReplyRequest body,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(traceId, ErrorCodes.ValidationFailed, "Content is required.");

        var thread = await repo.GetThreadByIdAsync(threadId);
        if (thread is null)
            return NotFound(traceId, ErrorCodes.ThreadNotFound, "Thread not found.");

        var (isMember, _) = await repo.CheckMembershipAsync(thread.NetworkId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var replyId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        var reply = new Reply
        {
            ReplyId = replyId,
            ThreadId = threadId,
            AuthorUserId = userId,
            Content = body.Content.Trim(),
            CreatedAt = now,
        };

        await repo.CreateReplyAsync(reply);

        // Detect @mentions and queue notifications
        var mentions = MentionRegex().Matches(body.Content);
        foreach (Match mention in mentions)
        {
            var mentionedUserId = mention.Groups[1].Value;
            if (mentionedUserId != userId)
            {
                try
                {
                    await repo.QueueMentionNotificationAsync(mentionedUserId, userId, threadId, body.Content);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to queue mention notification for userId={MentionedUserId}, traceId={TraceId}",
                        mentionedUserId, traceId);
                }
            }
        }

        logger.LogInformation("Reply created replyId={ReplyId}, threadId={ThreadId}, userId={UserId}, traceId={TraceId}",
            replyId, threadId, userId, traceId);

        return Results.Created($"/api/content/threads/{threadId}/replies/{replyId}", new ReplyResponse
        {
            ReplyId = replyId,
            ThreadId = threadId,
            AuthorUserId = userId,
            AuthorDisplayName = userId,
            Content = reply.Content,
            CreatedAt = now,
        });
    }

    private static async Task<IResult> HandleListReplies(
        string threadId,
        HttpRequest request,
        ContentRepository repo,
        [FromQuery] string? nextToken = null,
        [FromQuery] int limit = 50)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var thread = await repo.GetThreadByIdAsync(threadId);
        if (thread is null)
            return NotFound(traceId, ErrorCodes.ThreadNotFound, "Thread not found.");

        var (isMember, _) = await repo.CheckMembershipAsync(thread.NetworkId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var replies = await repo.ListRepliesAsync(threadId, nextToken, limit);

        return Results.Ok(new ReplyListResponse
        {
            Replies = replies.Select(r => new ReplyResponse
            {
                ReplyId = r.ReplyId,
                ThreadId = r.ThreadId,
                AuthorUserId = r.AuthorUserId,
                AuthorDisplayName = r.AuthorUserId,
                Content = r.Content,
                CreatedAt = r.CreatedAt,
            }).ToList(),
        });
    }

    private static async Task<IResult> HandleDeleteReply(
        string threadId,
        string replyId,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = ExtractUserId(request);
        if (userId is null) return Unauthorized(traceId);

        var thread = await repo.GetThreadByIdAsync(threadId);
        if (thread is null)
            return NotFound(traceId, ErrorCodes.ThreadNotFound, "Thread not found.");

        var (isMember, role) = await repo.CheckMembershipAsync(thread.NetworkId, userId);
        if (!isMember)
            return Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var replySk = await repo.FindReplySortKeyAsync(threadId, replyId);
        if (replySk is null)
            return NotFound(traceId, "REPLY_NOT_FOUND", "Reply not found.");

        var reply = await repo.GetReplyAsync(threadId, replyId);
        if (reply is null)
            return NotFound(traceId, "REPLY_NOT_FOUND", "Reply not found.");

        // Only author or moderator can delete
        var isAuthor = reply.AuthorUserId == userId;
        var permissions = await repo.GetRolePermissionsAsync(thread.NetworkId, role!);
        var isModerator = permissions.HasFlag(Permission.ModerateContent);

        if (!isAuthor && !isModerator)
            return Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You can only delete your own replies.");

        await repo.SoftDeleteReplyAsync(threadId, replyId, replySk);

        logger.LogInformation("Reply soft-deleted replyId={ReplyId}, threadId={ThreadId}, userId={UserId}, traceId={TraceId}",
            replyId, threadId, userId, traceId);

        return Results.Ok(new { Message = "Reply deleted." });
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

    internal static IResult Forbidden(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 403);

    internal static IResult BadRequest(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 400);

    [GeneratedRegex(@"@(\w{26,})")]
    private static partial Regex MentionRegex();
}

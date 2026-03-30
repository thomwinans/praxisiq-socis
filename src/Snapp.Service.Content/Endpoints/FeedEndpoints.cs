using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Content.Repositories;
using Snapp.Shared.Auth;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.Models;

namespace Snapp.Service.Content.Endpoints;

public static class FeedEndpoints
{
    public static void MapFeedEndpoints(this WebApplication app)
    {
        app.MapPost("/api/content/networks/{netId}/posts", HandleCreatePost)
            .WithName("CreatePost")
            .WithTags("Feed")
            .Accepts<CreatePostRequest>("application/json")
            .Produces<PostResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/content/networks/{netId}/feed", HandleListFeed)
            .WithName("ListNetworkFeed")
            .WithTags("Feed")
            .Produces<FeedResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(403)
            .WithOpenApi();

        app.MapGet("/api/content/users/{userId}/posts", HandleListUserPosts)
            .WithName("ListUserPosts")
            .WithTags("Feed")
            .Produces<FeedResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/content/posts/{postId}/react", HandleReact)
            .WithName("ReactToPost")
            .WithTags("Feed")
            .Produces<Dictionary<string, int>>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapDelete("/api/content/posts/{postId}/react", HandleRemoveReaction)
            .WithName("RemoveReaction")
            .WithTags("Feed")
            .Produces<Dictionary<string, int>>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleCreatePost(
        string netId,
        [FromBody] CreatePostRequest body,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = DiscussionEndpoints.ExtractUserId(request);
        if (userId is null) return DiscussionEndpoints.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Content))
            return DiscussionEndpoints.BadRequest(traceId, ErrorCodes.ValidationFailed, "Content is required.");

        var (isMember, role) = await repo.CheckMembershipAsync(netId, userId);
        if (!isMember)
            return DiscussionEndpoints.Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var permissions = await repo.GetRolePermissionsAsync(netId, role!);
        if (!permissions.HasFlag(Permission.CreatePost))
            return DiscussionEndpoints.Forbidden(traceId, ErrorCodes.InsufficientPermissions, "You do not have permission to create posts.");

        var postId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        var post = new Post
        {
            PostId = postId,
            NetworkId = netId,
            AuthorUserId = userId,
            Content = body.Content.Trim(),
            PostType = body.PostType,
            ReactionCounts = new Dictionary<string, int>(),
            CreatedAt = now,
        };

        await repo.CreatePostAsync(post);

        logger.LogInformation("Post created postId={PostId}, networkId={NetworkId}, userId={UserId}, traceId={TraceId}",
            postId, netId, userId, traceId);

        return Results.Created($"/api/content/posts/{postId}", new PostResponse
        {
            PostId = postId,
            NetworkId = netId,
            AuthorUserId = userId,
            AuthorDisplayName = userId,
            Content = post.Content,
            PostType = post.PostType,
            ReactionCounts = post.ReactionCounts,
            CreatedAt = now,
        });
    }

    private static async Task<IResult> HandleListFeed(
        string netId,
        HttpRequest request,
        ContentRepository repo,
        [FromQuery] string? nextToken = null,
        [FromQuery] int limit = 25)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = DiscussionEndpoints.ExtractUserId(request);
        if (userId is null) return DiscussionEndpoints.Unauthorized(traceId);

        var (isMember, _) = await repo.CheckMembershipAsync(netId, userId);
        if (!isMember)
            return DiscussionEndpoints.Forbidden(traceId, ErrorCodes.NotAMember, "You must be a member of this network.");

        var posts = await repo.ListNetworkFeedAsync(netId, nextToken, limit);

        return Results.Ok(new FeedResponse
        {
            Posts = posts.Select(p => new PostResponse
            {
                PostId = p.PostId,
                NetworkId = p.NetworkId,
                AuthorUserId = p.AuthorUserId,
                AuthorDisplayName = p.AuthorUserId,
                Content = p.Content,
                PostType = p.PostType,
                ReactionCounts = p.ReactionCounts,
                CreatedAt = p.CreatedAt,
            }).ToList(),
        });
    }

    private static async Task<IResult> HandleListUserPosts(
        string userId,
        HttpRequest request,
        ContentRepository repo,
        [FromQuery] string? nextToken = null,
        [FromQuery] int limit = 25)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var callerUserId = DiscussionEndpoints.ExtractUserId(request);
        if (callerUserId is null) return DiscussionEndpoints.Unauthorized(traceId);

        var posts = await repo.ListUserPostsAsync(userId, nextToken, limit);

        return Results.Ok(new FeedResponse
        {
            Posts = posts.Select(p => new PostResponse
            {
                PostId = p.PostId,
                NetworkId = p.NetworkId,
                AuthorUserId = p.AuthorUserId,
                AuthorDisplayName = p.AuthorUserId,
                Content = p.Content,
                PostType = p.PostType,
                ReactionCounts = p.ReactionCounts,
                CreatedAt = p.CreatedAt,
            }).ToList(),
        });
    }

    private static async Task<IResult> HandleReact(
        string postId,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = DiscussionEndpoints.ExtractUserId(request);
        if (userId is null) return DiscussionEndpoints.Unauthorized(traceId);

        // Read reaction type from body
        ReactionRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<ReactionRequest>();
        }
        catch
        {
            return DiscussionEndpoints.BadRequest(traceId, ErrorCodes.ValidationFailed, "Invalid request body.");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ReactionType))
            return DiscussionEndpoints.BadRequest(traceId, ErrorCodes.ValidationFailed, "ReactionType is required.");

        var validReactions = new[] { "like", "insightful", "support" };
        if (!validReactions.Contains(body.ReactionType.ToLowerInvariant()))
            return DiscussionEndpoints.BadRequest(traceId, ErrorCodes.ValidationFailed, "ReactionType must be: like, insightful, or support.");

        var post = await repo.GetPostByIdAsync(postId);
        if (post is null)
            return DiscussionEndpoints.NotFound(traceId, ErrorCodes.PostNotFound, "Post not found.");

        // Check if user already has a different reaction — remove old one first
        var existingReaction = await repo.GetReactionAsync(postId, userId);
        if (existingReaction is not null && existingReaction != body.ReactionType.ToLowerInvariant())
        {
            await repo.RemoveReactionAsync(postId, userId);
        }

        var counts = await repo.AddReactionAsync(postId, userId, body.ReactionType.ToLowerInvariant());

        logger.LogInformation("Reaction added postId={PostId}, userId={UserId}, type={ReactionType}, traceId={TraceId}",
            postId, userId, body.ReactionType, traceId);

        return Results.Ok(counts);
    }

    private static async Task<IResult> HandleRemoveReaction(
        string postId,
        HttpRequest request,
        ContentRepository repo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = DiscussionEndpoints.ExtractUserId(request);
        if (userId is null) return DiscussionEndpoints.Unauthorized(traceId);

        var post = await repo.GetPostByIdAsync(postId);
        if (post is null)
            return DiscussionEndpoints.NotFound(traceId, ErrorCodes.PostNotFound, "Post not found.");

        var counts = await repo.RemoveReactionAsync(postId, userId);

        logger.LogInformation("Reaction removed postId={PostId}, userId={UserId}, traceId={TraceId}",
            postId, userId, traceId);

        return Results.Ok(counts);
    }
}

public record ReactionRequest
{
    public string ReactionType { get; init; } = string.Empty;
}

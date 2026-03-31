using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.LinkedIn.Clients;
using Snapp.Service.LinkedIn.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.LinkedIn;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.LinkedIn.Endpoints;

public static class LinkedInEndpoints
{
    private const int ShareRateLimitPerDay = 25;

    public static void MapLinkedInEndpoints(this WebApplication app)
    {
        // OAuth endpoints
        app.MapGet("/api/linkedin/auth-url", HandleAuthUrl)
            .WithName("GetLinkedInAuthUrl")
            .WithTags("LinkedIn", "OAuth")
            .Produces<LinkedInAuthUrlResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/linkedin/callback", HandleCallback)
            .WithName("LinkedInCallback")
            .WithTags("LinkedIn", "OAuth")
            .Accepts<LinkedInCallbackRequest>("application/json")
            .Produces<LinkedInProfileResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/linkedin/status", HandleStatus)
            .WithName("GetLinkedInStatus")
            .WithTags("LinkedIn", "OAuth")
            .Produces<LinkedInStatusResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/linkedin/unlink", HandleUnlink)
            .WithName("UnlinkLinkedIn")
            .WithTags("LinkedIn", "OAuth")
            .Produces<MessageResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        // Share endpoint
        app.MapPost("/api/linkedin/share", HandleShare)
            .WithName("ShareToLinkedIn")
            .WithTags("LinkedIn", "Share")
            .Accepts<LinkedInShareRequest>("application/json")
            .Produces<LinkedInShareResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(429)
            .WithOpenApi();
    }

    private static IResult HandleAuthUrl(
        HttpContext httpContext,
        IConfiguration config,
        ILogger<LinkedInAuthUrlResponse> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = GetUserId(httpContext);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(traceId);

        var clientId = config["LinkedIn:ClientId"] ?? "mock-client-id";
        var redirectUri = config["LinkedIn:RedirectUri"] ?? "http://localhost:5000/linkedin/callback";
        var scope = "openid profile email w_member_social";

        // Generate CSRF state token: userId + random bytes
        var stateRaw = $"{userId}:{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateRaw));

        var authUrl = $"https://www.linkedin.com/oauth/v2/authorization" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state)}";

        logger.LogInformation("LinkedIn auth URL generated userId={UserId}, traceId={TraceId}",
            userId, traceId);

        return Results.Ok(new LinkedInAuthUrlResponse { AuthorizationUrl = authUrl });
    }

    private static async Task<IResult> HandleCallback(
        [FromBody] LinkedInCallbackRequest request,
        HttpContext httpContext,
        ILinkedInClient linkedInClient,
        ILinkedInRepository linkedInRepo,
        IUserRepository userRepo,
        IFieldEncryptor encryptor,
        IConfiguration config,
        ILogger<LinkedInCallbackRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = GetUserId(httpContext);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(request.Code))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "Authorization code is required",
                    TraceId = traceId,
                },
            });

        if (string.IsNullOrWhiteSpace(request.State))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "State token is required",
                    TraceId = traceId,
                },
            });

        // Validate CSRF state contains the current userId
        try
        {
            var stateDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(request.State));
            if (!stateDecoded.StartsWith($"{userId}:"))
            {
                logger.LogWarning("LinkedIn callback CSRF mismatch userId={UserId}, traceId={TraceId}",
                    userId, traceId);
                return Results.BadRequest(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Code = ErrorCodes.ValidationFailed,
                        Message = "Invalid state token",
                        TraceId = traceId,
                    },
                });
            }
        }
        catch (FormatException)
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "Invalid state token format",
                    TraceId = traceId,
                },
            });
        }

        // Exchange code for access token
        var redirectUri = config["LinkedIn:RedirectUri"] ?? "http://localhost:5000/linkedin/callback";
        var (accessToken, expiresIn) = await linkedInClient.ExchangeCodeForTokenAsync(request.Code, redirectUri);

        // Fetch LinkedIn profile
        var profile = await linkedInClient.GetProfileAsync(accessToken);

        // Encrypt token and URN
        var (encryptedToken, keyId) = await encryptor.EncryptWithKeyIdAsync(accessToken);
        var encryptedUrn = await encryptor.EncryptAsync(profile.Sub);

        // Store LinkedIn data
        var linkedInToken = new LinkedInToken
        {
            UserId = userId,
            EncryptedAccessToken = encryptedToken,
            EncryptedLinkedInURN = encryptedUrn,
            TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn),
            EncryptionKeyId = keyId,
        };

        await linkedInRepo.SaveAsync(linkedInToken);

        // Update user profile completeness (+15%) and LinkedIn URL
        var user = await userRepo.GetByIdAsync(userId);
        if (user is not null)
        {
            user.ProfileCompleteness = Math.Min(100, user.ProfileCompleteness + 15);
            user.LinkedInProfileUrl = $"https://www.linkedin.com/in/{profile.Sub}";
            user.UpdatedAt = DateTime.UtcNow;
            await userRepo.UpdateAsync(user);
        }

        logger.LogInformation(
            "LinkedIn linked userId={UserId}, name={Name}, traceId={TraceId}",
            userId, profile.Name, traceId);

        return Results.Ok(new LinkedInProfileResponse
        {
            LinkedInName = profile.Name,
            LinkedInHeadline = profile.Headline,
            PhotoUrl = profile.PhotoUrl,
        });
    }

    private static async Task<IResult> HandleStatus(
        HttpContext httpContext,
        ILinkedInRepository linkedInRepo,
        IFieldEncryptor encryptor,
        ILogger<LinkedInStatusResponse> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = GetUserId(httpContext);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(traceId);

        var token = await linkedInRepo.GetAsync(userId);

        if (token is null)
        {
            return Results.Ok(new LinkedInStatusResponse
            {
                IsLinked = false,
            });
        }

        // We don't store the name in DynamoDB — return linked status and expiry
        logger.LogInformation("LinkedIn status checked userId={UserId}, traceId={TraceId}",
            userId, traceId);

        return Results.Ok(new LinkedInStatusResponse
        {
            IsLinked = true,
            TokenExpiry = token.TokenExpiry,
        });
    }

    private static async Task<IResult> HandleUnlink(
        HttpContext httpContext,
        ILinkedInRepository linkedInRepo,
        IUserRepository userRepo,
        ILogger<MessageResponse> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = GetUserId(httpContext);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(traceId);

        await linkedInRepo.DeleteAsync(userId);

        // Update user profile: remove LinkedIn URL, reduce completeness
        var user = await userRepo.GetByIdAsync(userId);
        if (user is not null)
        {
            user.ProfileCompleteness = Math.Max(0, user.ProfileCompleteness - 15);
            user.LinkedInProfileUrl = null;
            user.UpdatedAt = DateTime.UtcNow;
            await userRepo.UpdateAsync(user);
        }

        logger.LogInformation("LinkedIn unlinked userId={UserId}, traceId={TraceId}",
            userId, traceId);

        return Results.Ok(new MessageResponse { Message = "LinkedIn account unlinked." });
    }

    private static async Task<IResult> HandleShare(
        [FromBody] LinkedInShareRequest request,
        HttpContext httpContext,
        ILinkedInClient linkedInClient,
        LinkedInRepository linkedInRepo,
        IFieldEncryptor encryptor,
        ILogger<LinkedInShareRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];
        var userId = GetUserId(httpContext);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "Content is required",
                    TraceId = traceId,
                },
            });

        // Check LinkedIn is linked
        var token = await linkedInRepo.GetAsync(userId);
        if (token is null)
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.LinkedInNotLinked,
                    Message = "LinkedIn account is not linked. Please link your account first.",
                    TraceId = traceId,
                },
            });

        // Check token not expired
        if (token.TokenExpiry < DateTime.UtcNow)
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.LinkedInTokenExpired,
                    Message = "LinkedIn token has expired. Please re-link your account.",
                    TraceId = traceId,
                },
            });

        // Rate limit: 25 shares/day/user
        var windowKey = DateTime.UtcNow.ToString("yyyyMMdd");
        var allowed = await linkedInRepo.TryIncrementRateLimitAsync(
            userId, windowKey, ShareRateLimitPerDay);

        if (!allowed)
        {
            logger.LogWarning("Share rate limit exceeded userId={UserId}, traceId={TraceId}",
                userId, traceId);
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.RateLimitExceeded,
                    Message = "Daily share limit reached (25/day). Try again tomorrow.",
                    TraceId = traceId,
                },
            }, statusCode: 429);
        }

        // Decrypt token and URN
        var accessToken = await encryptor.DecryptAsync(token.EncryptedAccessToken);
        var linkedInUrn = await encryptor.DecryptAsync(token.EncryptedLinkedInURN);

        // Format content with PraxisIQ attribution
        var deeplink = $"https://app.praxisiq.com/network/{request.NetworkId}";
        var formattedContent = $"{request.Content}\n\n— via PraxisIQ {deeplink}";

        // Publish to LinkedIn
        var postUrl = await linkedInClient.SharePostAsync(accessToken, linkedInUrn, formattedContent);

        logger.LogInformation(
            "LinkedIn share posted userId={UserId}, networkId={NetworkId}, traceId={TraceId}",
            userId, request.NetworkId, traceId);

        return Results.Ok(new LinkedInShareResponse { LinkedInPostUrl = postUrl });
    }

    private static string? GetUserId(HttpContext httpContext)
    {
        // Kong forwards the JWT claims as headers after verification.
        // The userId is in the "sub" claim, forwarded as X-Consumer-Custom-Id or extracted from JWT.
        return httpContext.Request.Headers["X-User-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Consumer-Custom-Id"].FirstOrDefault();
    }

    private static IResult Unauthorized(string traceId) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = ErrorCodes.Unauthorized,
                Message = "Authentication required.",
                TraceId = traceId,
            },
        }, statusCode: 401);
}

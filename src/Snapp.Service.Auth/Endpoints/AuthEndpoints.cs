using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Auth.Services;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Auth.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/magic-link", HandleMagicLink)
            .WithName("RequestMagicLink")
            .WithTags("Authentication")
            .Accepts<MagicLinkRequest>("application/json")
            .Produces<MessageResponse>(200)
            .Produces<ErrorResponse>(429)
            .Produces<ErrorResponse>(400)
            .WithOpenApi();

        app.MapPost("/api/auth/validate", HandleValidate)
            .WithName("ValidateMagicLink")
            .WithTags("Authentication")
            .Accepts<MagicLinkValidateRequest>("application/json")
            .Produces<TokenResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(400)
            .WithOpenApi();

        app.MapPost("/api/auth/refresh", HandleRefresh)
            .WithName("RefreshToken")
            .WithTags("Authentication")
            .Accepts<RefreshRequest>("application/json")
            .Produces<TokenResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/auth/logout", HandleLogout)
            .WithName("Logout")
            .WithTags("Authentication")
            .Accepts<RefreshRequest>("application/json")
            .Produces<MessageResponse>(200)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleMagicLink(
        [FromBody] MagicLinkRequest request,
        IAuthRepository authRepo,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<MagicLinkRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "Email is required",
                    TraceId = traceId,
                },
            });

        var email = request.Email.Trim().ToLowerInvariant();
        var hashedEmail = HashEmail(email);

        // Rate limit: 3 per email per 15-min window
        var windowKey = DateTime.UtcNow.ToString("yyyyMMddHH") +
            (DateTime.UtcNow.Minute / Limits.RateLimitWindowMinutes * Limits.RateLimitWindowMinutes)
                .ToString("D2");

        var allowed = await authRepo.TryIncrementRateLimitAsync(
            hashedEmail, windowKey, Limits.MagicLinkRateLimitPerWindow);

        if (!allowed)
        {
            logger.LogWarning("Rate limit exceeded for email hash {EmailHash}, traceId={TraceId}",
                hashedEmail[..8], traceId);
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.RateLimitExceeded,
                    Message = "Too many requests. Please try again later.",
                    TraceId = traceId,
                },
            }, statusCode: 429);
        }

        var code = JwtTokenService.GenerateMagicLinkCode();
        var now = DateTime.UtcNow;

        var token = new MagicLinkToken
        {
            Code = code,
            HashedEmail = hashedEmail,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(Limits.MagicLinkTtlMinutes),
        };

        await authRepo.CreateMagicLinkAsync(token);

        var baseUrl = config["Auth:MagicLinkBaseUrl"] ?? "http://localhost:5000";
        var magicLinkUrl = $"{baseUrl}/auth/verify?code={code}";

        try
        {
            await emailSender.SendAsync(
                email,
                "Your SNAPP Sign-In Link",
                BuildMagicLinkHtml(magicLinkUrl),
                $"Sign in to SNAPP: {magicLinkUrl}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send magic link email, traceId={TraceId}", traceId);
            // Return 200 regardless for security (don't reveal if email exists)
        }

        logger.LogInformation("Magic link requested for email hash {EmailHash}, traceId={TraceId}",
            hashedEmail[..8], traceId);

        // Always return 200 regardless of whether email exists (security)
        return Results.Ok(new MessageResponse { Message = "If this email is registered, a sign-in link has been sent." });
    }

    private static async Task<IResult> HandleValidate(
        [FromBody] MagicLinkValidateRequest request,
        IAuthRepository authRepo,
        IUserRepository userRepo,
        IFieldEncryptor encryptor,
        JwtTokenService jwtService,
        ILogger<MagicLinkValidateRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        if (string.IsNullOrWhiteSpace(request.Code))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "Code is required",
                    TraceId = traceId,
                },
            });

        // Look up token
        var token = await authRepo.GetMagicLinkAsync(request.Code);
        if (token is null)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidOrExpiredLink,
                    Message = "Invalid or expired magic link.",
                    TraceId = traceId,
                },
            }, statusCode: 401);
        }

        // Delete token (single-use)
        await authRepo.DeleteMagicLinkAsync(request.Code);

        // Look up user by email hash
        var hashedEmail = token.HashedEmail;
        var existingUserId = await userRepo.GetUserIdByEmailHashAsync(hashedEmail);
        var isNewUser = existingUserId is null;
        string userId;

        if (isNewUser)
        {
            // Create new user
            userId = Ulid.NewUlid().ToString();
            var now = DateTime.UtcNow;

            var user = new User
            {
                UserId = userId,
                DisplayName = "New User",
                ProfileCompleteness = 0,
                CreatedAt = now,
                UpdatedAt = now,
            };

            // We don't have the plaintext email here — we only have the hash.
            // Store encrypted placeholder; the user will provide email during onboarding.
            var (encryptedEmail, keyId) = await encryptor.EncryptWithKeyIdAsync($"user-{userId}@pending-verification");

            var pii = new UserPii
            {
                UserId = userId,
                EncryptedEmail = encryptedEmail,
                EncryptionKeyId = keyId,
            };

            await userRepo.CreateAsync(user, pii, hashedEmail);

            logger.LogInformation("New user created userId={UserId}, traceId={TraceId}", userId, traceId);
        }
        else
        {
            userId = existingUserId!;
        }

        // Generate JWT
        var accessToken = jwtService.GenerateAccessToken(userId, hashedEmail);

        // Generate refresh token
        var refreshTokenRaw = JwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = HashSha256(refreshTokenRaw);

        await authRepo.CreateRefreshTokenAsync(new RefreshToken
        {
            TokenHash = refreshTokenHash,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(Limits.RefreshTokenTtlDays),
        });

        logger.LogInformation("User authenticated userId={UserId}, isNew={IsNew}, traceId={TraceId}",
            userId, isNewUser, traceId);

        return Results.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenRaw,
            ExpiresIn = Limits.AccessTokenTtlMinutes * 60,
            IsNewUser = isNewUser,
        });
    }

    private static async Task<IResult> HandleRefresh(
        [FromBody] RefreshRequest request,
        IAuthRepository authRepo,
        JwtTokenService jwtService,
        ILogger<RefreshRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidRefreshToken,
                    Message = "Refresh token is required.",
                    TraceId = traceId,
                },
            }, statusCode: 401);

        var tokenHash = HashSha256(request.RefreshToken);
        var existing = await authRepo.GetRefreshTokenAsync(tokenHash);

        if (existing is null)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidRefreshToken,
                    Message = "Invalid or expired refresh token.",
                    TraceId = traceId,
                },
            }, statusCode: 401);
        }

        // Delete old refresh token (rotation)
        await authRepo.DeleteRefreshTokenAsync(tokenHash);

        // Issue new tokens
        var accessToken = jwtService.GenerateAccessToken(existing.UserId, null);
        var newRefreshRaw = JwtTokenService.GenerateRefreshToken();
        var newRefreshHash = HashSha256(newRefreshRaw);

        await authRepo.CreateRefreshTokenAsync(new RefreshToken
        {
            TokenHash = newRefreshHash,
            UserId = existing.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(Limits.RefreshTokenTtlDays),
        });

        logger.LogInformation("Token refreshed userId={UserId}, traceId={TraceId}",
            existing.UserId, traceId);

        return Results.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshRaw,
            ExpiresIn = Limits.AccessTokenTtlMinutes * 60,
            IsNewUser = false,
        });
    }

    private static async Task<IResult> HandleLogout(
        [FromBody] RefreshRequest request,
        IAuthRepository authRepo,
        ILogger<RefreshRequest> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenHash = HashSha256(request.RefreshToken);
            await authRepo.DeleteRefreshTokenAsync(tokenHash);
        }

        logger.LogInformation("Logout completed, traceId={TraceId}", traceId);

        return Results.Ok(new MessageResponse { Message = "Logged out successfully." });
    }

    private static string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes);
    }

    private static string HashSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static string BuildMagicLinkHtml(string url) => $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 20px;">
            <div style="max-width: 480px; margin: 0 auto;">
                <h2 style="color: #1a1a2e;">Sign in to SNAPP</h2>
                <p>Click the button below to sign in. This link expires in {Limits.MagicLinkTtlMinutes} minutes.</p>
                <a href="{url}"
                   style="display: inline-block; padding: 12px 24px; background-color: #6366f1;
                          color: white; text-decoration: none; border-radius: 6px; font-weight: 600;">
                    Sign In
                </a>
                <p style="color: #666; font-size: 14px; margin-top: 24px;">
                    If you didn't request this link, you can safely ignore this email.
                </p>
                <p style="color: #999; font-size: 12px;">
                    Or copy this link: {url}
                </p>
            </div>
        </body>
        </html>
        """;
}

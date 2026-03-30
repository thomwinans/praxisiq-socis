using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.User;
using Snapp.Shared.Interfaces;

namespace Snapp.Service.User.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/users/me", HandleGetMe)
            .WithName("GetMyProfile")
            .WithTags("Users")
            .Produces<ProfileResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/users/search", HandleSearch)
            .WithName("SearchUsers")
            .WithTags("Users")
            .Produces<List<ProfileResponse>>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapGet("/api/users/me/pii", HandleGetMyPii)
            .WithName("GetMyPii")
            .WithTags("Users")
            .Produces<PiiResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/users/{userId}", HandleGetUser)
            .WithName("GetUserProfile")
            .WithTags("Users")
            .Produces<ProfileResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapPut("/api/users/me", HandleUpdateMe)
            .WithName("UpdateMyProfile")
            .WithTags("Users")
            .Accepts<UpdateProfileRequest>("application/json")
            .Produces<ProfileResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(400)
            .WithOpenApi();

        app.MapPost("/api/users/me/onboard", HandleOnboard)
            .WithName("OnboardUser")
            .WithTags("Users")
            .Accepts<OnboardingRequest>("application/json")
            .Produces<ProfileResponse>(200)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(400)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetMe(
        HttpRequest request,
        IUserRepository userRepo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var user = await userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(traceId, ErrorCodes.UserNotFound, "User not found.");

        return Results.Ok(MapToResponse(user));
    }

    private static async Task<IResult> HandleGetUser(
        string userId,
        HttpRequest request,
        IUserRepository userRepo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var callerId = ExtractUserId(request);
        if (callerId is null)
            return Unauthorized(traceId);

        var user = await userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(traceId, ErrorCodes.UserNotFound, "User not found.");

        // Return public fields only (same DTO but no PII exposure)
        return Results.Ok(MapToResponse(user));
    }

    private static async Task<IResult> HandleUpdateMe(
        [FromBody] UpdateProfileRequest body,
        HttpRequest request,
        IUserRepository userRepo,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var user = await userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(traceId, ErrorCodes.UserNotFound, "User not found.");

        // Update only non-null fields
        if (body.DisplayName is not null) user.DisplayName = body.DisplayName;
        if (body.Specialty is not null) user.Specialty = body.Specialty;
        if (body.Geography is not null) user.Geography = body.Geography;
        if (body.PhotoUrl is not null) user.PhotoUrl = body.PhotoUrl;

        user.ProfileCompleteness = CalculateCompleteness(user);
        user.UpdatedAt = DateTime.UtcNow;

        await userRepo.UpdateAsync(user);

        logger.LogInformation("Profile updated userId={UserId}, completeness={Completeness}, traceId={TraceId}",
            userId, user.ProfileCompleteness, traceId);

        return Results.Ok(MapToResponse(user));
    }

    private static async Task<IResult> HandleOnboard(
        [FromBody] OnboardingRequest body,
        HttpRequest request,
        IUserRepository userRepo,
        IFieldEncryptor encryptor,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.Email))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "DisplayName and Email are required.",
                    TraceId = traceId,
                },
            });

        var user = await userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(traceId, ErrorCodes.UserNotFound, "User not found.");

        // Update profile fields
        user.DisplayName = body.DisplayName;
        if (body.Specialty is not null) user.Specialty = body.Specialty;
        if (body.Geography is not null) user.Geography = body.Geography;
        if (body.LinkedInProfileUrl is not null) user.LinkedInProfileUrl = body.LinkedInProfileUrl;

        user.ProfileCompleteness = CalculateCompleteness(user);
        user.UpdatedAt = DateTime.UtcNow;

        await userRepo.UpdateAsync(user);

        // Encrypt and store PII
        var (encryptedEmail, keyId) = await encryptor.EncryptWithKeyIdAsync(body.Email.Trim().ToLowerInvariant());
        string? encryptedPhone = null;
        if (!string.IsNullOrWhiteSpace(body.Phone))
            encryptedPhone = await encryptor.EncryptAsync(body.Phone);

        var pii = new Snapp.Shared.Models.UserPii
        {
            UserId = userId,
            EncryptedEmail = encryptedEmail,
            EncryptedPhone = encryptedPhone,
            EncryptionKeyId = keyId,
        };

        await userRepo.UpdatePiiAsync(pii);

        logger.LogInformation("User onboarded userId={UserId}, completeness={Completeness}, traceId={TraceId}",
            userId, user.ProfileCompleteness, traceId);

        return Results.Ok(MapToResponse(user));
    }

    private static async Task<IResult> HandleGetMyPii(
        HttpRequest request,
        IUserRepository userRepo,
        IFieldEncryptor encryptor,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        var pii = await userRepo.GetPiiAsync(userId);
        if (pii is null)
            return NotFound(traceId, ErrorCodes.UserNotFound, "PII not found.");

        var email = await encryptor.DecryptAsync(pii.EncryptedEmail);
        string? phone = pii.EncryptedPhone is not null
            ? await encryptor.DecryptAsync(pii.EncryptedPhone)
            : null;
        string? contactInfo = pii.EncryptedContactInfo is not null
            ? await encryptor.DecryptAsync(pii.EncryptedContactInfo)
            : null;

        return Results.Ok(new PiiResponse
        {
            Email = email,
            Phone = phone,
            ContactInfo = contactInfo,
        });
    }

    private static async Task<IResult> HandleSearch(
        [FromQuery] string? specialty,
        [FromQuery] string? geo,
        IUserRepository userRepo,
        HttpRequest request,
        ILogger<Program> logger)
    {
        var traceId = Guid.NewGuid().ToString("N")[..16];

        var userId = ExtractUserId(request);
        if (userId is null)
            return Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(specialty))
            return Results.BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "specialty query parameter is required.",
                    TraceId = traceId,
                },
            });

        var users = await userRepo.SearchBySpecialtyGeoAsync(specialty, geo ?? "", null);
        var results = users.Select(MapToResponse).ToList();

        return Results.Ok(results);
    }

    /// <summary>
    /// Extracts the userId from the JWT 'sub' claim, forwarded by Kong in the
    /// X-Consumer-Username or X-Credential-Username header, or directly from
    /// the Authorization bearer token.
    /// </summary>
    private static string? ExtractUserId(HttpRequest request)
    {
        // Kong forwards authenticated consumer info in headers
        // But for JWT, the claims are in the token itself.
        // Kong jwt plugin forwards the token; we extract 'sub' from it.
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

    /// <summary>
    /// ProfileCompleteness calculation:
    /// DisplayName +20, Specialty +20, Geography +20, LinkedIn +15, PracticeData +15, Photo +10.
    /// </summary>
    internal static decimal CalculateCompleteness(Snapp.Shared.Models.User user)
    {
        decimal score = 0;
        if (!string.IsNullOrWhiteSpace(user.DisplayName) && user.DisplayName != "New User") score += 20;
        if (!string.IsNullOrWhiteSpace(user.Specialty)) score += 20;
        if (!string.IsNullOrWhiteSpace(user.Geography)) score += 20;
        if (!string.IsNullOrWhiteSpace(user.LinkedInProfileUrl)) score += 15;
        if (user.HasPracticeData) score += 15;
        if (!string.IsNullOrWhiteSpace(user.PhotoUrl)) score += 10;
        return score;
    }

    private static ProfileResponse MapToResponse(Snapp.Shared.Models.User user) => new()
    {
        UserId = user.UserId,
        DisplayName = user.DisplayName,
        Specialty = user.Specialty,
        Geography = user.Geography,
        LinkedInProfileUrl = user.LinkedInProfileUrl,
        PhotoUrl = user.PhotoUrl,
        HasPracticeData = user.HasPracticeData,
        ProfileCompleteness = user.ProfileCompleteness,
        CreatedAt = user.CreatedAt,
    };

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

    private static IResult NotFound(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                TraceId = traceId,
            },
        }, statusCode: 404);
}

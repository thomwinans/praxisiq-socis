using System.IdentityModel.Tokens.Jwt;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Service.Intelligence.Endpoints;

public static class EndpointHelpers
{
    public static string? ExtractUserId(HttpRequest request)
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

    public static IResult Unauthorized(string traceId) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = ErrorCodes.Unauthorized,
                Message = "Authentication required.",
                TraceId = traceId,
            },
        }, statusCode: 401);

    public static IResult NotFound(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 404);

    public static IResult BadRequest(string traceId, string code, string message) =>
        Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message, TraceId = traceId },
        }, statusCode: 400);

    public static string NewTraceId() => Guid.NewGuid().ToString("N")[..16];
}

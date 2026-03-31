namespace Snapp.Shared.Constants;

/// <summary>
/// Standardized error codes returned in <see cref="DTOs.Common.ErrorResponse"/>.
/// Error codes are stable API surface — do not rename without a breaking change notice.
/// See TRD Section 8.1.
/// </summary>
public static class ErrorCodes
{
    // Auth (401)
    public const string InvalidOrExpiredLink = "INVALID_OR_EXPIRED_LINK";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string Unauthorized = "UNAUTHORIZED";

    // Forbidden (403)
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
    public const string NotAMember = "NOT_A_MEMBER";
    public const string NotAParticipant = "NOT_A_PARTICIPANT";

    // Not Found (404)
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string NetworkNotFound = "NETWORK_NOT_FOUND";
    public const string PostNotFound = "POST_NOT_FOUND";
    public const string ThreadNotFound = "THREAD_NOT_FOUND";
    public const string ReferralNotFound = "REFERRAL_NOT_FOUND";
    public const string DealRoomNotFound = "DEAL_ROOM_NOT_FOUND";
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string NotificationNotFound = "NOTIFICATION_NOT_FOUND";
    public const string ValuationNotFound = "VALUATION_NOT_FOUND";
    public const string CareerStageNotFound = "CAREER_STAGE_NOT_FOUND";

    // Rate Limit (429)
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    // Validation (400)
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string ApplicationAlreadyExists = "APPLICATION_ALREADY_EXISTS";
    public const string AlreadyAMember = "ALREADY_A_MEMBER";
    public const string LinkedInNotLinked = "LINKEDIN_NOT_LINKED";
    public const string LinkedInTokenExpired = "LINKEDIN_TOKEN_EXPIRED";
    public const string InvalidApplicationDecision = "INVALID_APPLICATION_DECISION";
    public const string InvalidReferralStatus = "INVALID_REFERRAL_STATUS";

    // Server (500)
    public const string InternalError = "INTERNAL_ERROR";
    public const string EncryptionError = "ENCRYPTION_ERROR";
    public const string EmailDeliveryFailed = "EMAIL_DELIVERY_FAILED";
}

namespace Snapp.Shared.Constants;

public static class Limits
{
    public const int MaxPostLength = 5000;
    public const int MaxThreadTitleLength = 200;
    public const int MaxReplyLength = 5000;
    public const int MaxNetworkNameLength = 100;
    public const int MaxNetworkDescriptionLength = 2000;
    public const int MaxApplicationTextLength = 1000;
    public const int MaxFeedPageSize = 25;
    public const int MaxMemberPageSize = 50;
    public const int MagicLinkTtlMinutes = 15;
    public const int RefreshTokenTtlDays = 30;
    public const int AccessTokenTtlMinutes = 15;
    public const int MagicLinkRateLimitPerWindow = 3;
    public const int RateLimitWindowMinutes = 15;
}

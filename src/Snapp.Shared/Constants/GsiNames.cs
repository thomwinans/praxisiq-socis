namespace Snapp.Shared.Constants;

/// <summary>
/// Global Secondary Index names used across DynamoDB tables.
/// These names are used in both Pulumi table creation and repository query code.
/// </summary>
public static class GsiNames
{
    // snapp-users
    public const string Email = "GSI-Email";
    public const string Specialty = "GSI-Specialty";

    // snapp-networks
    public const string UserNetworks = "GSI-UserNetworks";
    public const string PendingApps = "GSI-PendingApps";

    // snapp-content
    public const string UserPosts = "GSI-UserPosts";

    // snapp-intel
    public const string BenchmarkLookup = "GSI-BenchmarkLookup";
    public const string RiskFlags = "GSI-RiskFlags";

    // snapp-tx
    public const string UserReferrals = "GSI-UserReferrals";
    public const string OpenReferrals = "GSI-OpenReferrals";

    // snapp-notif
    public const string UndigestedNotifs = "GSI-UndigestedNotifs";
    public const string DigestQueue = "GSI-DigestQueue";
}

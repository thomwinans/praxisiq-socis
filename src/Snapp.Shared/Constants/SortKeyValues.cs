namespace Snapp.Shared.Constants;

/// <summary>
/// Static sort key values used across DynamoDB tables.
/// Complements <see cref="KeyPrefixes"/> which defines PK/SK prefixes.
/// </summary>
public static class SortKeyValues
{
    // snapp-users table
    public const string Profile = "PROFILE";
    public const string Pii = "PII";
    public const string User = "USER";
    public const string MagicLink = "MAGIC_LINK";
    public const string Session = "SESSION";
    public const string LinkedIn = "LINKEDIN";
    public const string NotifPrefs = "NOTIF_PREFS";

    // snapp-networks table
    public const string Meta = "META";

    // snapp-intel table
    public const string Current = "CURRENT";

    // snapp-tx table (deal room reuses Meta from above)
}

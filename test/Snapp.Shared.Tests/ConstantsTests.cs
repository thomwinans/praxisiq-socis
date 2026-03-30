using Xunit;
using Snapp.Shared.Constants;

namespace Snapp.Shared.Tests;

public class TableNamesTests
{
    [Theory]
    [InlineData(nameof(TableNames.Users), "snapp-users")]
    [InlineData(nameof(TableNames.Networks), "snapp-networks")]
    [InlineData(nameof(TableNames.Content), "snapp-content")]
    [InlineData(nameof(TableNames.Intelligence), "snapp-intel")]
    [InlineData(nameof(TableNames.Transactions), "snapp-tx")]
    [InlineData(nameof(TableNames.Notifications), "snapp-notif")]
    public void TableNames_Value_MatchesExpected(string fieldName, string expected)
    {
        var actual = typeof(TableNames).GetField(fieldName)!.GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TableNames_AllStartWithSnappPrefix()
    {
        var fields = typeof(TableNames).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = f.GetValue(null) as string;
            Assert.StartsWith("snapp-", value);
        });
    }

    [Fact]
    public void TableNames_HasSixTables()
    {
        var fields = typeof(TableNames).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.Equal(6, fields.Length);
    }
}

public class KeyPrefixesTests
{
    [Theory]
    [InlineData(nameof(KeyPrefixes.User), "USER#")]
    [InlineData(nameof(KeyPrefixes.Email), "EMAIL#")]
    [InlineData(nameof(KeyPrefixes.Token), "TOKEN#")]
    [InlineData(nameof(KeyPrefixes.Refresh), "REFRESH#")]
    [InlineData(nameof(KeyPrefixes.Rate), "RATE#")]
    [InlineData(nameof(KeyPrefixes.Network), "NET#")]
    [InlineData(nameof(KeyPrefixes.UserMembership), "UMEM#")]
    [InlineData(nameof(KeyPrefixes.AppStatus), "APPSTATUS#")]
    [InlineData(nameof(KeyPrefixes.Feed), "FEED#")]
    [InlineData(nameof(KeyPrefixes.UserPost), "UPOST#")]
    [InlineData(nameof(KeyPrefixes.Discussion), "DISC#")]
    [InlineData(nameof(KeyPrefixes.Thread), "THREAD#")]
    [InlineData(nameof(KeyPrefixes.Reaction), "REACT#")]
    [InlineData(nameof(KeyPrefixes.PracticeData), "PDATA#")]
    [InlineData(nameof(KeyPrefixes.Score), "SCORE#")]
    [InlineData(nameof(KeyPrefixes.Stage), "STAGE#")]
    [InlineData(nameof(KeyPrefixes.Valuation), "VAL#")]
    [InlineData(nameof(KeyPrefixes.Benchmark), "BENCH#")]
    [InlineData(nameof(KeyPrefixes.Cohort), "COHORT#")]
    [InlineData(nameof(KeyPrefixes.Signal), "SIGNAL#")]
    [InlineData(nameof(KeyPrefixes.Market), "MKT#")]
    [InlineData(nameof(KeyPrefixes.ScoringDimension), "DIMDEF#")]
    [InlineData(nameof(KeyPrefixes.Risk), "RISK#")]
    [InlineData(nameof(KeyPrefixes.QuestionPending), "QPEND#")]
    [InlineData(nameof(KeyPrefixes.QuestionAnswered), "QANS#")]
    [InlineData(nameof(KeyPrefixes.Unlock), "UNLOCK#")]
    [InlineData(nameof(KeyPrefixes.Progression), "PROG#")]
    [InlineData(nameof(KeyPrefixes.Referral), "REF#")]
    [InlineData(nameof(KeyPrefixes.UserReferral), "UREF#")]
    [InlineData(nameof(KeyPrefixes.RefStatus), "REFSTATUS#")]
    [InlineData(nameof(KeyPrefixes.Reputation), "REP#")]
    [InlineData(nameof(KeyPrefixes.Attestation), "ATTEST#")]
    [InlineData(nameof(KeyPrefixes.Deal), "DEAL#")]
    [InlineData(nameof(KeyPrefixes.Notification), "NOTIF#")]
    [InlineData(nameof(KeyPrefixes.Digest), "DIGEST#")]
    [InlineData(nameof(KeyPrefixes.DigestQueue), "DQUEUE#")]
    public void KeyPrefixes_Value_MatchesExpected(string fieldName, string expected)
    {
        var actual = typeof(KeyPrefixes).GetField(fieldName)!.GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void KeyPrefixes_AllEndWithHash()
    {
        var fields = typeof(KeyPrefixes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = f.GetValue(null) as string;
            Assert.EndsWith("#", value);
        });
    }

    [Fact]
    public void KeyPrefixes_AllUppercase()
    {
        var fields = typeof(KeyPrefixes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = (f.GetValue(null) as string)!.TrimEnd('#');
            Assert.Equal(value.ToUpperInvariant(), value);
        });
    }

    [Fact]
    public void KeyPrefixes_NoDuplicateValues()
    {
        var fields = typeof(KeyPrefixes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var values = fields.Select(f => f.GetValue(null) as string).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}

public class LimitsTests
{
    [Theory]
    [InlineData(nameof(Limits.MaxPostLength), 5000)]
    [InlineData(nameof(Limits.MaxThreadTitleLength), 200)]
    [InlineData(nameof(Limits.MaxReplyLength), 5000)]
    [InlineData(nameof(Limits.MaxNetworkNameLength), 100)]
    [InlineData(nameof(Limits.MaxNetworkDescriptionLength), 2000)]
    [InlineData(nameof(Limits.MaxApplicationTextLength), 1000)]
    [InlineData(nameof(Limits.MaxFeedPageSize), 25)]
    [InlineData(nameof(Limits.MaxMemberPageSize), 50)]
    [InlineData(nameof(Limits.MagicLinkTtlMinutes), 15)]
    [InlineData(nameof(Limits.RefreshTokenTtlDays), 30)]
    [InlineData(nameof(Limits.AccessTokenTtlMinutes), 15)]
    [InlineData(nameof(Limits.MagicLinkRateLimitPerWindow), 3)]
    [InlineData(nameof(Limits.RateLimitWindowMinutes), 15)]
    public void Limits_Value_MatchesExpected(string fieldName, int expected)
    {
        var actual = (int)typeof(Limits).GetField(fieldName)!.GetValue(null)!;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Limits_AllPositive()
    {
        var fields = typeof(Limits).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = (int)f.GetValue(null)!;
            Assert.True(value > 0, $"{f.Name} must be positive, was {value}");
        });
    }
}

public class ErrorCodesTests
{
    [Theory]
    // Auth (401)
    [InlineData(nameof(ErrorCodes.InvalidOrExpiredLink), "INVALID_OR_EXPIRED_LINK")]
    [InlineData(nameof(ErrorCodes.InvalidRefreshToken), "INVALID_REFRESH_TOKEN")]
    [InlineData(nameof(ErrorCodes.TokenExpired), "TOKEN_EXPIRED")]
    [InlineData(nameof(ErrorCodes.Unauthorized), "UNAUTHORIZED")]
    // Forbidden (403)
    [InlineData(nameof(ErrorCodes.InsufficientPermissions), "INSUFFICIENT_PERMISSIONS")]
    [InlineData(nameof(ErrorCodes.NotAMember), "NOT_A_MEMBER")]
    [InlineData(nameof(ErrorCodes.NotAParticipant), "NOT_A_PARTICIPANT")]
    // Not Found (404)
    [InlineData(nameof(ErrorCodes.UserNotFound), "USER_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.NetworkNotFound), "NETWORK_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.PostNotFound), "POST_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.ThreadNotFound), "THREAD_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.ReferralNotFound), "REFERRAL_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.DealRoomNotFound), "DEAL_ROOM_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.DocumentNotFound), "DOCUMENT_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.NotificationNotFound), "NOTIFICATION_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.ValuationNotFound), "VALUATION_NOT_FOUND")]
    // Rate Limit (429)
    [InlineData(nameof(ErrorCodes.RateLimitExceeded), "RATE_LIMIT_EXCEEDED")]
    // Validation (400)
    [InlineData(nameof(ErrorCodes.ValidationFailed), "VALIDATION_FAILED")]
    [InlineData(nameof(ErrorCodes.DuplicateEmail), "DUPLICATE_EMAIL")]
    [InlineData(nameof(ErrorCodes.ApplicationAlreadyExists), "APPLICATION_ALREADY_EXISTS")]
    [InlineData(nameof(ErrorCodes.AlreadyAMember), "ALREADY_A_MEMBER")]
    [InlineData(nameof(ErrorCodes.LinkedInNotLinked), "LINKEDIN_NOT_LINKED")]
    [InlineData(nameof(ErrorCodes.LinkedInTokenExpired), "LINKEDIN_TOKEN_EXPIRED")]
    [InlineData(nameof(ErrorCodes.InvalidApplicationDecision), "INVALID_APPLICATION_DECISION")]
    [InlineData(nameof(ErrorCodes.InvalidReferralStatus), "INVALID_REFERRAL_STATUS")]
    // Server (500)
    [InlineData(nameof(ErrorCodes.InternalError), "INTERNAL_ERROR")]
    [InlineData(nameof(ErrorCodes.EncryptionError), "ENCRYPTION_ERROR")]
    [InlineData(nameof(ErrorCodes.EmailDeliveryFailed), "EMAIL_DELIVERY_FAILED")]
    public void ErrorCodes_Value_MatchesExpected(string fieldName, string expected)
    {
        var actual = typeof(ErrorCodes).GetField(fieldName)!.GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ErrorCodes_AllUpperSnakeCase()
    {
        var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = f.GetValue(null) as string;
            Assert.Matches(@"^[A-Z][A-Z0-9_]+$", value);
        });
    }

    [Fact]
    public void ErrorCodes_NoDuplicateValues()
    {
        var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var values = fields.Select(f => f.GetValue(null) as string).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}

public class GsiNamesTests
{
    [Theory]
    [InlineData(nameof(GsiNames.Email), "GSI-Email")]
    [InlineData(nameof(GsiNames.Specialty), "GSI-Specialty")]
    [InlineData(nameof(GsiNames.UserNetworks), "GSI-UserNetworks")]
    [InlineData(nameof(GsiNames.PendingApps), "GSI-PendingApps")]
    [InlineData(nameof(GsiNames.UserPosts), "GSI-UserPosts")]
    [InlineData(nameof(GsiNames.BenchmarkLookup), "GSI-BenchmarkLookup")]
    [InlineData(nameof(GsiNames.RiskFlags), "GSI-RiskFlags")]
    [InlineData(nameof(GsiNames.UserReferrals), "GSI-UserReferrals")]
    [InlineData(nameof(GsiNames.OpenReferrals), "GSI-OpenReferrals")]
    [InlineData(nameof(GsiNames.UndigestedNotifs), "GSI-UndigestedNotifs")]
    [InlineData(nameof(GsiNames.DigestQueue), "GSI-DigestQueue")]
    public void GsiNames_Value_MatchesExpected(string fieldName, string expected)
    {
        var actual = typeof(GsiNames).GetField(fieldName)!.GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GsiNames_AllStartWithGsiPrefix()
    {
        var fields = typeof(GsiNames).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = f.GetValue(null) as string;
            Assert.StartsWith("GSI-", value);
        });
    }

    [Fact]
    public void GsiNames_NoDuplicateValues()
    {
        var fields = typeof(GsiNames).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var values = fields.Select(f => f.GetValue(null) as string).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}

public class SortKeyValuesTests
{
    [Theory]
    [InlineData(nameof(SortKeyValues.Profile), "PROFILE")]
    [InlineData(nameof(SortKeyValues.Pii), "PII")]
    [InlineData(nameof(SortKeyValues.User), "USER")]
    [InlineData(nameof(SortKeyValues.MagicLink), "MAGIC_LINK")]
    [InlineData(nameof(SortKeyValues.Session), "SESSION")]
    [InlineData(nameof(SortKeyValues.LinkedIn), "LINKEDIN")]
    [InlineData(nameof(SortKeyValues.NotifPrefs), "NOTIF_PREFS")]
    [InlineData(nameof(SortKeyValues.Meta), "META")]
    [InlineData(nameof(SortKeyValues.Current), "CURRENT")]
    public void SortKeyValues_Value_MatchesExpected(string fieldName, string expected)
    {
        var actual = typeof(SortKeyValues).GetField(fieldName)!.GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SortKeyValues_AllUpperCase()
    {
        var fields = typeof(SortKeyValues).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.All(fields, f =>
        {
            var value = f.GetValue(null) as string;
            Assert.Equal(value!.ToUpperInvariant(), value);
        });
    }
}

public class ClaimTypesTests
{
    [Fact]
    public void ClaimTypes_UserId_IsSub()
    {
        Assert.Equal("sub", Snapp.Shared.Auth.ClaimTypes.UserId);
    }

    [Fact]
    public void ClaimTypes_Email_IsEmail()
    {
        Assert.Equal("email", Snapp.Shared.Auth.ClaimTypes.Email);
    }

    [Fact]
    public void ClaimTypes_Roles_IsRoles()
    {
        Assert.Equal("roles", Snapp.Shared.Auth.ClaimTypes.Roles);
    }
}

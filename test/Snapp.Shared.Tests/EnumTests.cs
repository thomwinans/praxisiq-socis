using System.Text.Json;
using Xunit;
using Snapp.Shared.Auth;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Tests;

public class PermissionTests
{
    [Theory]
    [InlineData(Permission.None, 0)]
    [InlineData(Permission.ViewMembers, 1)]
    [InlineData(Permission.ManageMembers, 2)]
    [InlineData(Permission.CreatePost, 4)]
    [InlineData(Permission.ModerateContent, 8)]
    [InlineData(Permission.ManageNetwork, 16)]
    [InlineData(Permission.ManageRoles, 32)]
    [InlineData(Permission.ReviewApplications, 64)]
    [InlineData(Permission.ViewIntelligence, 128)]
    [InlineData(Permission.ManageReferrals, 256)]
    [InlineData(Permission.ManageDealRooms, 512)]
    [InlineData(Permission.Admin, int.MaxValue)]
    public void Permission_Value_MatchesExpected(Permission permission, int expected)
    {
        Assert.Equal(expected, (int)permission);
    }

    [Fact]
    public void Permission_IsFlagsEnum()
    {
        Assert.NotNull(typeof(Permission).GetCustomAttributes(typeof(FlagsAttribute), false).FirstOrDefault());
    }

    [Fact]
    public void Permission_FlagsCombineCorrectly()
    {
        var combined = Permission.ViewMembers | Permission.CreatePost;
        Assert.True(combined.HasFlag(Permission.ViewMembers));
        Assert.True(combined.HasFlag(Permission.CreatePost));
        Assert.False(combined.HasFlag(Permission.ManageMembers));
    }

    [Fact]
    public void Permission_AdminIncludesAllFlags()
    {
        var allFlags = Enum.GetValues<Permission>()
            .Where(p => p != Permission.Admin && p != Permission.None)
            .Aggregate(Permission.None, (a, b) => a | b);

        Assert.True(Permission.Admin.HasFlag(allFlags));
    }

    [Fact]
    public void Permission_NoneHasNoFlags()
    {
        var allFlags = Enum.GetValues<Permission>()
            .Where(p => p != Permission.Admin && p != Permission.None);

        foreach (var flag in allFlags)
            Assert.False(Permission.None.HasFlag(flag));
    }

    [Fact]
    public void Permission_ValuesArePowersOfTwo()
    {
        var values = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None && p != Permission.Admin)
            .Select(p => (int)p);

        Assert.All(values, v => Assert.True((v & (v - 1)) == 0, $"Value {v} is not a power of two"));
    }

    [Theory]
    [InlineData(Permission.ViewMembers)]
    [InlineData(Permission.ManageMembers | Permission.CreatePost)]
    [InlineData(Permission.Admin)]
    public void Permission_SerializesAsInteger(Permission permission)
    {
        var json = JsonSerializer.Serialize(permission);
        var deserialized = JsonSerializer.Deserialize<Permission>(json);
        Assert.Equal(permission, deserialized);
    }
}

public class MembershipStatusTests
{
    [Theory]
    [InlineData(MembershipStatus.Active, 0)]
    [InlineData(MembershipStatus.Suspended, 1)]
    [InlineData(MembershipStatus.Emeritus, 2)]
    public void MembershipStatus_Value_MatchesExpected(MembershipStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Fact]
    public void MembershipStatus_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<MembershipStatus>().Length);
    }

    [Theory]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Emeritus)]
    public void MembershipStatus_RoundTripsJson(MembershipStatus status)
    {
        var json = JsonSerializer.Serialize(status);
        var deserialized = JsonSerializer.Deserialize<MembershipStatus>(json);
        Assert.Equal(status, deserialized);
    }
}

public class PostTypeTests
{
    [Theory]
    [InlineData(PostType.Text, 0)]
    [InlineData(PostType.Milestone, 1)]
    [InlineData(PostType.Poll, 2)]
    public void PostType_Value_MatchesExpected(PostType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Fact]
    public void PostType_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<PostType>().Length);
    }

    [Theory]
    [InlineData(PostType.Text)]
    [InlineData(PostType.Milestone)]
    [InlineData(PostType.Poll)]
    public void PostType_RoundTripsJson(PostType type)
    {
        var json = JsonSerializer.Serialize(type);
        var deserialized = JsonSerializer.Deserialize<PostType>(json);
        Assert.Equal(type, deserialized);
    }
}

public class ReferralStatusTests
{
    [Theory]
    [InlineData(ReferralStatus.Created, 0)]
    [InlineData(ReferralStatus.Accepted, 1)]
    [InlineData(ReferralStatus.Completed, 2)]
    [InlineData(ReferralStatus.Expired, 3)]
    public void ReferralStatus_Value_MatchesExpected(ReferralStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Fact]
    public void ReferralStatus_HasFourValues()
    {
        Assert.Equal(4, Enum.GetValues<ReferralStatus>().Length);
    }

    [Theory]
    [InlineData(ReferralStatus.Created)]
    [InlineData(ReferralStatus.Accepted)]
    [InlineData(ReferralStatus.Completed)]
    [InlineData(ReferralStatus.Expired)]
    public void ReferralStatus_RoundTripsJson(ReferralStatus status)
    {
        var json = JsonSerializer.Serialize(status);
        var deserialized = JsonSerializer.Deserialize<ReferralStatus>(json);
        Assert.Equal(status, deserialized);
    }
}

public class DealStatusTests
{
    [Theory]
    [InlineData(DealStatus.Active, 0)]
    [InlineData(DealStatus.Closed, 1)]
    [InlineData(DealStatus.Archived, 2)]
    public void DealStatus_Value_MatchesExpected(DealStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Fact]
    public void DealStatus_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<DealStatus>().Length);
    }

    [Theory]
    [InlineData(DealStatus.Active)]
    [InlineData(DealStatus.Closed)]
    [InlineData(DealStatus.Archived)]
    public void DealStatus_RoundTripsJson(DealStatus status)
    {
        var json = JsonSerializer.Serialize(status);
        var deserialized = JsonSerializer.Deserialize<DealStatus>(json);
        Assert.Equal(status, deserialized);
    }
}

public class NotificationTypeTests
{
    [Theory]
    [InlineData(NotificationType.ReferralReceived, 0)]
    [InlineData(NotificationType.ReferralOutcome, 1)]
    [InlineData(NotificationType.MentionInDiscussion, 2)]
    [InlineData(NotificationType.ApplicationReceived, 3)]
    [InlineData(NotificationType.ApplicationDecision, 4)]
    [InlineData(NotificationType.MilestoneAchieved, 5)]
    [InlineData(NotificationType.ValuationChanged, 6)]
    [InlineData(NotificationType.NewNetworkMember, 7)]
    [InlineData(NotificationType.DigestSummary, 8)]
    public void NotificationType_Value_MatchesExpected(NotificationType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Fact]
    public void NotificationType_HasNineValues()
    {
        Assert.Equal(9, Enum.GetValues<NotificationType>().Length);
    }

    [Theory]
    [InlineData(NotificationType.ReferralReceived)]
    [InlineData(NotificationType.DigestSummary)]
    public void NotificationType_RoundTripsJson(NotificationType type)
    {
        var json = JsonSerializer.Serialize(type);
        var deserialized = JsonSerializer.Deserialize<NotificationType>(json);
        Assert.Equal(type, deserialized);
    }
}

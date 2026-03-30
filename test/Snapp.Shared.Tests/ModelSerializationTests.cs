using System.Text.Json;
using Xunit;
using Snapp.Shared.Auth;
using Snapp.Shared.Enums;
using Snapp.Shared.Models;

namespace Snapp.Shared.Tests;

/// <summary>
/// Every model must round-trip through System.Text.Json without data loss.
/// </summary>
public class ModelSerializationTests
{
    private static T RoundTrip<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var result = JsonSerializer.Deserialize<T>(json);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public void User_RoundTrips()
    {
        var user = new User
        {
            UserId = "u-123",
            DisplayName = "Jane Doe",
            Specialty = "Orthodontics",
            Geography = "Denver, CO",
            ProfileCompleteness = 85.5m,
            CreatedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var result = RoundTrip(user);
        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(user.Specialty, result.Specialty);
        Assert.Equal(user.Geography, result.Geography);
        Assert.Equal(user.ProfileCompleteness, result.ProfileCompleteness);
        Assert.Equal(user.CreatedAt, result.CreatedAt);
        Assert.Equal(user.UpdatedAt, result.UpdatedAt);
    }

    [Fact]
    public void UserPii_RoundTrips()
    {
        var pii = new UserPii
        {
            UserId = "u-123",
            EncryptedEmail = "enc-email-abc",
            EncryptedPhone = "enc-phone-xyz",
            EncryptedContactInfo = "enc-contact-def",
            EncryptionKeyId = "key-001"
        };

        var result = RoundTrip(pii);
        Assert.Equal(pii.UserId, result.UserId);
        Assert.Equal(pii.EncryptedEmail, result.EncryptedEmail);
        Assert.Equal(pii.EncryptedPhone, result.EncryptedPhone);
        Assert.Equal(pii.EncryptedContactInfo, result.EncryptedContactInfo);
        Assert.Equal(pii.EncryptionKeyId, result.EncryptionKeyId);
    }

    [Fact]
    public void Network_RoundTrips()
    {
        var network = new Network
        {
            NetworkId = "net-456",
            Name = "Denver Dental Network",
            Description = "A network for Denver dentists",
            Charter = "We believe in collaboration.",
            CreatedByUserId = "u-123",
            MemberCount = 42,
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(network);
        Assert.Equal(network.NetworkId, result.NetworkId);
        Assert.Equal(network.Name, result.Name);
        Assert.Equal(network.Description, result.Description);
        Assert.Equal(network.Charter, result.Charter);
        Assert.Equal(network.CreatedByUserId, result.CreatedByUserId);
        Assert.Equal(network.MemberCount, result.MemberCount);
    }

    [Fact]
    public void NetworkMembership_RoundTrips()
    {
        var membership = new NetworkMembership
        {
            NetworkId = "net-456",
            UserId = "u-123",
            Role = "owner",
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow,
            ContributionScore = 95.3m
        };

        var result = RoundTrip(membership);
        Assert.Equal(membership.NetworkId, result.NetworkId);
        Assert.Equal(membership.UserId, result.UserId);
        Assert.Equal(membership.Role, result.Role);
        Assert.Equal(membership.Status, result.Status);
        Assert.Equal(membership.ContributionScore, result.ContributionScore);
    }

    [Fact]
    public void NetworkRole_RoundTrips()
    {
        var role = new NetworkRole
        {
            RoleName = "moderator",
            Permissions = Permission.ViewMembers | Permission.ModerateContent | Permission.CreatePost,
            Description = "Can moderate content"
        };

        var result = RoundTrip(role);
        Assert.Equal(role.RoleName, result.RoleName);
        Assert.Equal(role.Permissions, result.Permissions);
        Assert.Equal(role.Description, result.Description);
    }

    [Fact]
    public void Post_RoundTrips()
    {
        var post = new Post
        {
            PostId = "p-789",
            NetworkId = "net-456",
            AuthorUserId = "u-123",
            Content = "Hello, SNAPP world!",
            PostType = PostType.Milestone,
            ReactionCounts = new Dictionary<string, int> { { "👍", 5 }, { "🎉", 3 } },
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(post);
        Assert.Equal(post.PostId, result.PostId);
        Assert.Equal(post.NetworkId, result.NetworkId);
        Assert.Equal(post.AuthorUserId, result.AuthorUserId);
        Assert.Equal(post.Content, result.Content);
        Assert.Equal(post.PostType, result.PostType);
        Assert.Equal(post.ReactionCounts["👍"], result.ReactionCounts["👍"]);
        Assert.Equal(post.ReactionCounts["🎉"], result.ReactionCounts["🎉"]);
    }

    [Fact]
    public void DiscussionThread_RoundTrips()
    {
        var thread = new DiscussionThread
        {
            ThreadId = "t-001",
            NetworkId = "net-456",
            Title = "Best practices for patient referrals",
            AuthorUserId = "u-123",
            ReplyCount = 12,
            LastReplyAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(thread);
        Assert.Equal(thread.ThreadId, result.ThreadId);
        Assert.Equal(thread.NetworkId, result.NetworkId);
        Assert.Equal(thread.Title, result.Title);
        Assert.Equal(thread.AuthorUserId, result.AuthorUserId);
        Assert.Equal(thread.ReplyCount, result.ReplyCount);
        Assert.Equal(thread.LastReplyAt, result.LastReplyAt);
    }

    [Fact]
    public void Reply_RoundTrips()
    {
        var reply = new Reply
        {
            ReplyId = "r-001",
            ThreadId = "t-001",
            AuthorUserId = "u-456",
            Content = "Great question!",
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(reply);
        Assert.Equal(reply.ReplyId, result.ReplyId);
        Assert.Equal(reply.ThreadId, result.ThreadId);
        Assert.Equal(reply.AuthorUserId, result.AuthorUserId);
        Assert.Equal(reply.Content, result.Content);
    }

    [Fact]
    public void Referral_RoundTrips()
    {
        var referral = new Referral
        {
            ReferralId = "ref-001",
            SenderUserId = "u-123",
            ReceiverUserId = "u-456",
            NetworkId = "net-789",
            Specialty = "Periodontics",
            Status = ReferralStatus.Accepted,
            Notes = "Urgent case",
            CreatedAt = DateTime.UtcNow,
            OutcomeRecordedAt = DateTime.UtcNow
        };

        var result = RoundTrip(referral);
        Assert.Equal(referral.ReferralId, result.ReferralId);
        Assert.Equal(referral.SenderUserId, result.SenderUserId);
        Assert.Equal(referral.ReceiverUserId, result.ReceiverUserId);
        Assert.Equal(referral.NetworkId, result.NetworkId);
        Assert.Equal(referral.Specialty, result.Specialty);
        Assert.Equal(referral.Status, result.Status);
        Assert.Equal(referral.Notes, result.Notes);
        Assert.Equal(referral.OutcomeRecordedAt, result.OutcomeRecordedAt);
    }

    [Fact]
    public void Referral_NullableOutcomeRecordedAt_RoundTrips()
    {
        var referral = new Referral
        {
            ReferralId = "ref-002",
            SenderUserId = "u-123",
            ReceiverUserId = "u-456",
            NetworkId = "net-789",
            OutcomeRecordedAt = null
        };

        var result = RoundTrip(referral);
        Assert.Null(result.OutcomeRecordedAt);
    }

    [Fact]
    public void Reputation_RoundTrips()
    {
        var rep = new Reputation
        {
            UserId = "u-123",
            OverallScore = 87.5m,
            ReferralScore = 92.0m,
            ContributionScore = 78.3m,
            AttestationScore = 95.1m,
            ComputedAt = DateTime.UtcNow
        };

        var result = RoundTrip(rep);
        Assert.Equal(rep.UserId, result.UserId);
        Assert.Equal(rep.OverallScore, result.OverallScore);
        Assert.Equal(rep.ReferralScore, result.ReferralScore);
        Assert.Equal(rep.ContributionScore, result.ContributionScore);
        Assert.Equal(rep.AttestationScore, result.AttestationScore);
    }

    [Fact]
    public void PracticeData_RoundTrips()
    {
        var data = new PracticeData
        {
            UserId = "u-123",
            Dimension = "Financial",
            Category = "Revenue",
            DataPoints = new Dictionary<string, string>
            {
                { "annual_revenue", "1200000" },
                { "ebitda", "300000" }
            },
            ConfidenceContribution = 12.5m,
            SubmittedAt = DateTime.UtcNow,
            Source = "manual"
        };

        var result = RoundTrip(data);
        Assert.Equal(data.UserId, result.UserId);
        Assert.Equal(data.Dimension, result.Dimension);
        Assert.Equal(data.Category, result.Category);
        Assert.Equal(data.DataPoints["annual_revenue"], result.DataPoints["annual_revenue"]);
        Assert.Equal(data.ConfidenceContribution, result.ConfidenceContribution);
        Assert.Equal(data.Source, result.Source);
    }

    [Fact]
    public void Valuation_RoundTrips()
    {
        var val = new Valuation
        {
            UserId = "u-123",
            Downside = 500000m,
            Base = 750000m,
            Upside = 1000000m,
            ConfidenceScore = 72.5m,
            Drivers = new Dictionary<string, string>
            {
                { "revenue_growth", "positive" },
                { "market_conditions", "neutral" }
            },
            Multiple = 3.2m,
            EbitdaMargin = 0.25m,
            ComputedAt = DateTime.UtcNow
        };

        var result = RoundTrip(val);
        Assert.Equal(val.UserId, result.UserId);
        Assert.Equal(val.Downside, result.Downside);
        Assert.Equal(val.Base, result.Base);
        Assert.Equal(val.Upside, result.Upside);
        Assert.Equal(val.ConfidenceScore, result.ConfidenceScore);
        Assert.Equal(val.Drivers["revenue_growth"], result.Drivers["revenue_growth"]);
        Assert.Equal(val.Multiple, result.Multiple);
        Assert.Equal(val.EbitdaMargin, result.EbitdaMargin);
    }

    [Fact]
    public void Benchmark_RoundTrips()
    {
        var bench = new Benchmark
        {
            Vertical = "Dental",
            Geography = "Colorado",
            GeographicLevel = "State",
            Specialty = "General",
            SizeBand = "1-5",
            MetricName = "Revenue per Provider",
            P25 = 400000m,
            P50 = 550000m,
            P75 = 700000m,
            Mean = 560000m,
            SampleSize = 150,
            ComputedAt = DateTime.UtcNow
        };

        var result = RoundTrip(bench);
        Assert.Equal(bench.Vertical, result.Vertical);
        Assert.Equal(bench.Geography, result.Geography);
        Assert.Equal(bench.MetricName, result.MetricName);
        Assert.Equal(bench.P25, result.P25);
        Assert.Equal(bench.P50, result.P50);
        Assert.Equal(bench.P75, result.P75);
        Assert.Equal(bench.Mean, result.Mean);
        Assert.Equal(bench.SampleSize, result.SampleSize);
    }

    [Fact]
    public void Notification_RoundTrips()
    {
        var notif = new Notification
        {
            NotificationId = "n-001",
            UserId = "u-123",
            Type = NotificationType.ReferralReceived,
            Category = "Referrals",
            Title = "New referral",
            Body = "You received a referral from Dr. Smith",
            SourceEntityId = "ref-001",
            IsRead = false,
            IsDigested = false,
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(notif);
        Assert.Equal(notif.NotificationId, result.NotificationId);
        Assert.Equal(notif.UserId, result.UserId);
        Assert.Equal(notif.Type, result.Type);
        Assert.Equal(notif.Category, result.Category);
        Assert.Equal(notif.Title, result.Title);
        Assert.Equal(notif.Body, result.Body);
        Assert.Equal(notif.SourceEntityId, result.SourceEntityId);
        Assert.Equal(notif.IsRead, result.IsRead);
        Assert.Equal(notif.IsDigested, result.IsDigested);
    }

    [Fact]
    public void NotificationPreferences_RoundTrips()
    {
        var prefs = new NotificationPreferences
        {
            UserId = "u-123",
            DigestTime = "08:30",
            Timezone = "America/Denver",
            ImmediateTypes = new List<NotificationType>
            {
                NotificationType.ReferralReceived,
                NotificationType.ApplicationDecision
            }
        };

        var result = RoundTrip(prefs);
        Assert.Equal(prefs.UserId, result.UserId);
        Assert.Equal(prefs.DigestTime, result.DigestTime);
        Assert.Equal(prefs.Timezone, result.Timezone);
        Assert.Equal(2, result.ImmediateTypes.Count);
        Assert.Contains(NotificationType.ReferralReceived, result.ImmediateTypes);
        Assert.Contains(NotificationType.ApplicationDecision, result.ImmediateTypes);
    }

    [Fact]
    public void NotificationPreferences_Defaults()
    {
        var prefs = new NotificationPreferences();
        Assert.Equal("07:00", prefs.DigestTime);
        Assert.Equal("America/New_York", prefs.Timezone);
        Assert.Empty(prefs.ImmediateTypes);
    }

    [Fact]
    public void DealRoom_RoundTrips()
    {
        var deal = new DealRoom
        {
            DealId = "deal-001",
            Name = "Practice Acquisition Q1",
            CreatedByUserId = "u-123",
            Status = DealStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(deal);
        Assert.Equal(deal.DealId, result.DealId);
        Assert.Equal(deal.Name, result.Name);
        Assert.Equal(deal.CreatedByUserId, result.CreatedByUserId);
        Assert.Equal(deal.Status, result.Status);
    }

    [Fact]
    public void DealParticipant_RoundTrips()
    {
        var participant = new DealParticipant
        {
            DealId = "deal-001",
            UserId = "u-456",
            Role = "buyer",
            AddedAt = DateTime.UtcNow
        };

        var result = RoundTrip(participant);
        Assert.Equal(participant.DealId, result.DealId);
        Assert.Equal(participant.UserId, result.UserId);
        Assert.Equal(participant.Role, result.Role);
    }

    [Fact]
    public void DealDocument_RoundTrips()
    {
        var doc = new DealDocument
        {
            DocumentId = "doc-001",
            DealId = "deal-001",
            Filename = "financials.pdf",
            S3Key = "deals/deal-001/financials.pdf",
            UploadedByUserId = "u-123",
            Size = 1048576,
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(doc);
        Assert.Equal(doc.DocumentId, result.DocumentId);
        Assert.Equal(doc.DealId, result.DealId);
        Assert.Equal(doc.Filename, result.Filename);
        Assert.Equal(doc.S3Key, result.S3Key);
        Assert.Equal(doc.UploadedByUserId, result.UploadedByUserId);
        Assert.Equal(doc.Size, result.Size);
    }

    [Fact]
    public void DealAuditEntry_RoundTrips()
    {
        var entry = new DealAuditEntry
        {
            EventId = "evt-001",
            DealId = "deal-001",
            Action = "document_uploaded",
            ActorUserId = "u-123",
            Details = "financials.pdf",
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(entry);
        Assert.Equal(entry.EventId, result.EventId);
        Assert.Equal(entry.DealId, result.DealId);
        Assert.Equal(entry.Action, result.Action);
        Assert.Equal(entry.ActorUserId, result.ActorUserId);
        Assert.Equal(entry.Details, result.Details);
    }

    [Fact]
    public void MagicLinkToken_RoundTrips()
    {
        var token = new MagicLinkToken
        {
            Code = new string('a', 64),
            HashedEmail = "sha256-hash-value",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        var result = RoundTrip(token);
        Assert.Equal(token.Code, result.Code);
        Assert.Equal(token.HashedEmail, result.HashedEmail);
        Assert.Equal(token.ExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public void RefreshToken_RoundTrips()
    {
        var token = new RefreshToken
        {
            TokenHash = "sha256-refresh-hash",
            UserId = "u-123",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var result = RoundTrip(token);
        Assert.Equal(token.TokenHash, result.TokenHash);
        Assert.Equal(token.UserId, result.UserId);
        Assert.Equal(token.ExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public void RateLimitWindow_RoundTrips()
    {
        var window = new RateLimitWindow
        {
            HashedEmail = "sha256-hash",
            WindowKey = "202503301200",
            Count = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        var result = RoundTrip(window);
        Assert.Equal(window.HashedEmail, result.HashedEmail);
        Assert.Equal(window.WindowKey, result.WindowKey);
        Assert.Equal(window.Count, result.Count);
        Assert.Equal(window.ExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public void LinkedInToken_RoundTrips()
    {
        var token = new LinkedInToken
        {
            UserId = "u-123",
            EncryptedLinkedInURN = "enc-urn-abc",
            EncryptedAccessToken = "enc-token-xyz",
            TokenExpiry = DateTime.UtcNow.AddDays(60),
            EncryptionKeyId = "key-002"
        };

        var result = RoundTrip(token);
        Assert.Equal(token.UserId, result.UserId);
        Assert.Equal(token.EncryptedLinkedInURN, result.EncryptedLinkedInURN);
        Assert.Equal(token.EncryptedAccessToken, result.EncryptedAccessToken);
        Assert.Equal(token.TokenExpiry, result.TokenExpiry);
        Assert.Equal(token.EncryptionKeyId, result.EncryptionKeyId);
    }

    [Fact]
    public void ChannelRelay_RoundTrips()
    {
        var relay = new ChannelRelay
        {
            ChannelId = "ch-001",
            NetworkId = "net-456",
            EncryptedChannelEmail = "enc-email-abc",
            Platform = "Teams",
            Label = "General Channel",
            IsVerified = true,
            RelayTypes = new List<string> { "posts", "milestones", "digest" },
            CreatedAt = DateTime.UtcNow
        };

        var result = RoundTrip(relay);
        Assert.Equal(relay.ChannelId, result.ChannelId);
        Assert.Equal(relay.NetworkId, result.NetworkId);
        Assert.Equal(relay.EncryptedChannelEmail, result.EncryptedChannelEmail);
        Assert.Equal(relay.Platform, result.Platform);
        Assert.Equal(relay.Label, result.Label);
        Assert.Equal(relay.IsVerified, result.IsVerified);
        Assert.Equal(3, result.RelayTypes.Count);
        Assert.Contains("posts", result.RelayTypes);
    }
}

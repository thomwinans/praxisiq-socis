using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.Shared.DTOs.LinkedIn;
using Snapp.Shared.DTOs.Network;
using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.DTOs.User;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Tests;

/// <summary>
/// Validates DataAnnotations attributes on all DTOs.
/// </summary>
public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object obj)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(obj);
        Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object obj) => Validate(obj).Count == 0;

    // ──────────────────────────────────────────────
    // Auth DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void MagicLinkRequest_Valid_Passes()
    {
        var req = new MagicLinkRequest { Email = "test@example.com" };
        Assert.True(IsValid(req));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.com")]
    public void MagicLinkRequest_InvalidEmail_Fails(string email)
    {
        var req = new MagicLinkRequest { Email = email };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void MagicLinkRequest_EmailExceedsMaxLength_Fails()
    {
        var req = new MagicLinkRequest { Email = new string('a', 246) + "@test.com" }; // 255 chars, > 254
        Assert.False(IsValid(req));
    }

    [Fact]
    public void MagicLinkValidateRequest_Valid_Passes()
    {
        var req = new MagicLinkValidateRequest { Code = new string('x', 64) };
        Assert.True(IsValid(req));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void MagicLinkValidateRequest_TooShort_Fails(string code)
    {
        var req = new MagicLinkValidateRequest { Code = code };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void MagicLinkValidateRequest_TooLong_Fails()
    {
        var req = new MagicLinkValidateRequest { Code = new string('x', 129) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void RefreshRequest_Valid_Passes()
    {
        var req = new RefreshRequest { RefreshToken = "some-token" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void RefreshRequest_EmptyToken_Fails()
    {
        var req = new RefreshRequest { RefreshToken = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void TokenResponse_RoundTrips()
    {
        var resp = new TokenResponse
        {
            AccessToken = "jwt-abc",
            RefreshToken = "ref-xyz",
            ExpiresIn = 900,
            IsNewUser = true
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<TokenResponse>(json)!;
        Assert.Equal(resp.AccessToken, result.AccessToken);
        Assert.Equal(resp.RefreshToken, result.RefreshToken);
        Assert.Equal(resp.ExpiresIn, result.ExpiresIn);
        Assert.True(result.IsNewUser);
    }

    // ──────────────────────────────────────────────
    // Common DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void ErrorResponse_RoundTrips()
    {
        var err = new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "VALIDATION_FAILED",
                Message = "Name is required",
                TraceId = "trace-123"
            }
        };

        var json = JsonSerializer.Serialize(err);
        var result = JsonSerializer.Deserialize<ErrorResponse>(json)!;
        Assert.Equal(err.Error.Code, result.Error.Code);
        Assert.Equal(err.Error.Message, result.Error.Message);
        Assert.Equal(err.Error.TraceId, result.Error.TraceId);
    }

    [Fact]
    public void MessageResponse_RoundTrips()
    {
        var msg = new MessageResponse { Message = "Magic link sent" };
        var json = JsonSerializer.Serialize(msg);
        var result = JsonSerializer.Deserialize<MessageResponse>(json)!;
        Assert.Equal(msg.Message, result.Message);
    }

    // ──────────────────────────────────────────────
    // User DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void CreateProfileRequest_Valid_Passes()
    {
        var req = new CreateProfileRequest { DisplayName = "Jane Doe" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateProfileRequest_EmptyDisplayName_Fails()
    {
        var req = new CreateProfileRequest { DisplayName = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateProfileRequest_DisplayNameExceedsMax_Fails()
    {
        var req = new CreateProfileRequest { DisplayName = new string('a', 101) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateProfileRequest_SpecialtyExceedsMax_Fails()
    {
        var req = new CreateProfileRequest
        {
            DisplayName = "Jane",
            Specialty = new string('a', 101)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateProfileRequest_GeographyExceedsMax_Fails()
    {
        var req = new CreateProfileRequest
        {
            DisplayName = "Jane",
            Geography = new string('a', 201)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void UpdateProfileRequest_AllNull_IsValid()
    {
        var req = new UpdateProfileRequest();
        Assert.True(IsValid(req));
    }

    [Fact]
    public void UpdateProfileRequest_DisplayNameExceedsMax_Fails()
    {
        var req = new UpdateProfileRequest { DisplayName = new string('a', 101) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void OnboardingRequest_Valid_Passes()
    {
        var req = new OnboardingRequest
        {
            DisplayName = "Jane Doe",
            Email = "jane@example.com"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void OnboardingRequest_MissingDisplayName_Fails()
    {
        var req = new OnboardingRequest
        {
            DisplayName = "",
            Email = "jane@example.com"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void OnboardingRequest_InvalidEmail_Fails()
    {
        var req = new OnboardingRequest
        {
            DisplayName = "Jane Doe",
            Email = "not-an-email"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void OnboardingRequest_PhoneExceedsMax_Fails()
    {
        var req = new OnboardingRequest
        {
            DisplayName = "Jane Doe",
            Email = "jane@example.com",
            Phone = new string('1', 21)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void OnboardingRequest_LinkedInUrlExceedsMax_Fails()
    {
        var req = new OnboardingRequest
        {
            DisplayName = "Jane Doe",
            Email = "jane@example.com",
            LinkedInProfileUrl = "https://" + new string('a', 500)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void ProfileResponse_RoundTrips()
    {
        var resp = new ProfileResponse
        {
            UserId = "u-123",
            DisplayName = "Jane",
            Specialty = "Ortho",
            Geography = "Denver",
            ProfileCompleteness = 85.0m,
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<ProfileResponse>(json)!;
        Assert.Equal(resp.UserId, result.UserId);
        Assert.Equal(resp.DisplayName, result.DisplayName);
        Assert.Equal(resp.ProfileCompleteness, result.ProfileCompleteness);
    }

    // ──────────────────────────────────────────────
    // Network DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void CreateNetworkRequest_Valid_Passes()
    {
        var req = new CreateNetworkRequest { Name = "Denver Dental" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateNetworkRequest_EmptyName_Fails()
    {
        var req = new CreateNetworkRequest { Name = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateNetworkRequest_NameExceedsMax_Fails()
    {
        var req = new CreateNetworkRequest { Name = new string('a', 101) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateNetworkRequest_DescriptionExceedsMax_Fails()
    {
        var req = new CreateNetworkRequest
        {
            Name = "Valid",
            Description = new string('a', 2001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateNetworkRequest_CharterExceedsMax_Fails()
    {
        var req = new CreateNetworkRequest
        {
            Name = "Valid",
            Charter = new string('a', 10001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void UpdateNetworkRequest_AllNull_IsValid()
    {
        var req = new UpdateNetworkRequest();
        Assert.True(IsValid(req));
    }

    [Fact]
    public void UpdateNetworkRequest_NameExceedsMax_Fails()
    {
        var req = new UpdateNetworkRequest { Name = new string('a', 101) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void ApplyRequest_Valid_Passes()
    {
        var req = new ApplyRequest { NetworkId = "net-456" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void ApplyRequest_EmptyNetworkId_Fails()
    {
        var req = new ApplyRequest { NetworkId = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void ApplyRequest_ApplicationTextExceedsMax_Fails()
    {
        var req = new ApplyRequest
        {
            NetworkId = "net-456",
            ApplicationText = new string('a', 1001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void ApplicationDecisionRequest_Approved_Passes()
    {
        var req = new ApplicationDecisionRequest
        {
            UserId = "u-123",
            Decision = "Approved"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void ApplicationDecisionRequest_Denied_Passes()
    {
        var req = new ApplicationDecisionRequest
        {
            UserId = "u-123",
            Decision = "Denied"
        };
        Assert.True(IsValid(req));
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("denied")]
    [InlineData("Maybe")]
    [InlineData("")]
    public void ApplicationDecisionRequest_InvalidDecision_Fails(string decision)
    {
        var req = new ApplicationDecisionRequest
        {
            UserId = "u-123",
            Decision = decision
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void ApplicationDecisionRequest_ReasonExceedsMax_Fails()
    {
        var req = new ApplicationDecisionRequest
        {
            UserId = "u-123",
            Decision = "Approved",
            Reason = new string('a', 1001)
        };
        Assert.False(IsValid(req));
    }

    // ──────────────────────────────────────────────
    // Content DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void CreatePostRequest_Valid_Passes()
    {
        var req = new CreatePostRequest
        {
            NetworkId = "net-456",
            Content = "Hello world"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreatePostRequest_EmptyContent_Fails()
    {
        var req = new CreatePostRequest
        {
            NetworkId = "net-456",
            Content = ""
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreatePostRequest_ContentExceedsMax_Fails()
    {
        var req = new CreatePostRequest
        {
            NetworkId = "net-456",
            Content = new string('a', 5001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreatePostRequest_DefaultPostType_IsText()
    {
        var req = new CreatePostRequest();
        Assert.Equal(PostType.Text, req.PostType);
    }

    [Fact]
    public void CreateThreadRequest_Valid_Passes()
    {
        var req = new CreateThreadRequest
        {
            NetworkId = "net-456",
            Title = "Discussion topic",
            Content = "Let's talk about this"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateThreadRequest_TitleExceedsMax_Fails()
    {
        var req = new CreateThreadRequest
        {
            NetworkId = "net-456",
            Title = new string('a', 201),
            Content = "Body"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateThreadRequest_ContentExceedsMax_Fails()
    {
        var req = new CreateThreadRequest
        {
            NetworkId = "net-456",
            Title = "Valid",
            Content = new string('a', 10001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateReplyRequest_Valid_Passes()
    {
        var req = new CreateReplyRequest
        {
            ThreadId = "t-001",
            Content = "Great point!"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateReplyRequest_ContentExceedsMax_Fails()
    {
        var req = new CreateReplyRequest
        {
            ThreadId = "t-001",
            Content = new string('a', 5001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void FeedResponse_RoundTrips()
    {
        var feed = new FeedResponse
        {
            Posts = new List<PostResponse>
            {
                new() { PostId = "p-1", Content = "First post" }
            },
            NextToken = "token-abc"
        };

        var json = JsonSerializer.Serialize(feed);
        var result = JsonSerializer.Deserialize<FeedResponse>(json)!;
        Assert.Single(result.Posts);
        Assert.Equal("token-abc", result.NextToken);
    }

    // ──────────────────────────────────────────────
    // Intelligence DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void SubmitDataRequest_Valid_Passes()
    {
        var req = new SubmitDataRequest
        {
            Category = "Revenue",
            DataPoints = new Dictionary<string, string> { { "annual", "1000000" } }
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void SubmitDataRequest_EmptyCategory_Fails()
    {
        var req = new SubmitDataRequest
        {
            Category = "",
            DataPoints = new Dictionary<string, string> { { "key", "val" } }
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void SubmitDataRequest_CategoryExceedsMax_Fails()
    {
        var req = new SubmitDataRequest
        {
            Category = new string('a', 101),
            DataPoints = new Dictionary<string, string> { { "key", "val" } }
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void BenchmarkRequest_Valid_Passes()
    {
        var req = new BenchmarkRequest
        {
            Specialty = "General",
            Geography = "Colorado",
            SizeBand = "1-5"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void BenchmarkRequest_EmptySpecialty_Fails()
    {
        var req = new BenchmarkRequest
        {
            Specialty = "",
            Geography = "Colorado",
            SizeBand = "1-5"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void BenchmarkRequest_SpecialtyExceedsMax_Fails()
    {
        var req = new BenchmarkRequest
        {
            Specialty = new string('a', 101),
            Geography = "Colorado",
            SizeBand = "1-5"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void BenchmarkRequest_GeographyExceedsMax_Fails()
    {
        var req = new BenchmarkRequest
        {
            Specialty = "General",
            Geography = new string('a', 201),
            SizeBand = "1-5"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void BenchmarkRequest_SizeBandExceedsMax_Fails()
    {
        var req = new BenchmarkRequest
        {
            Specialty = "General",
            Geography = "Colorado",
            SizeBand = new string('a', 51)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void DashboardResponse_RoundTrips()
    {
        var resp = new DashboardResponse
        {
            KPIs = new List<KpiItem> { new() { Name = "Revenue", Value = "$1M", Trend = "Up" } },
            ConfidenceScore = 85.0m,
            ValuationSummary = new ValuationSummary
            {
                Downside = 500000m,
                Base = 750000m,
                Upside = 1000000m,
                ConfidenceScore = 72.5m
            }
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<DashboardResponse>(json)!;
        Assert.Single(result.KPIs);
        Assert.Equal(85.0m, result.ConfidenceScore);
        Assert.NotNull(result.ValuationSummary);
        Assert.Equal(750000m, result.ValuationSummary.Base);
    }

    [Fact]
    public void ValuationResponse_RoundTrips()
    {
        var resp = new ValuationResponse
        {
            Downside = 500000m,
            Base = 750000m,
            Upside = 1000000m,
            ConfidenceScore = 72.5m,
            Drivers = new List<ValuationDriver>
            {
                new() { Name = "Revenue", Impact = "High", Direction = "Positive" }
            },
            History = new List<ValuationSnapshot>
            {
                new() { Date = DateTime.UtcNow, Base = 700000m, ConfidenceScore = 70.0m }
            }
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<ValuationResponse>(json)!;
        Assert.Single(result.Drivers);
        Assert.Single(result.History);
        Assert.Equal(750000m, result.Base);
    }

    // ──────────────────────────────────────────────
    // LinkedIn DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void LinkedInCallbackRequest_Valid_Passes()
    {
        var req = new LinkedInCallbackRequest { Code = "auth-code", State = "csrf-state" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void LinkedInCallbackRequest_EmptyCode_Fails()
    {
        var req = new LinkedInCallbackRequest { Code = "", State = "csrf-state" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void LinkedInCallbackRequest_EmptyState_Fails()
    {
        var req = new LinkedInCallbackRequest { Code = "auth-code", State = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void LinkedInShareRequest_Valid_Passes()
    {
        var req = new LinkedInShareRequest
        {
            Content = "Check out our network!",
            NetworkId = "net-456",
            SourceType = "post"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void LinkedInShareRequest_ContentExceedsMax_Fails()
    {
        var req = new LinkedInShareRequest
        {
            Content = new string('a', 3001),
            NetworkId = "net-456",
            SourceType = "post"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void LinkedInAuthUrlResponse_RoundTrips()
    {
        var resp = new LinkedInAuthUrlResponse { AuthorizationUrl = "https://linkedin.com/oauth" };
        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<LinkedInAuthUrlResponse>(json)!;
        Assert.Equal(resp.AuthorizationUrl, result.AuthorizationUrl);
    }

    [Fact]
    public void LinkedInStatusResponse_RoundTrips()
    {
        var resp = new LinkedInStatusResponse
        {
            IsLinked = true,
            LinkedInName = "Jane Doe",
            TokenExpiry = DateTime.UtcNow.AddDays(60)
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<LinkedInStatusResponse>(json)!;
        Assert.True(result.IsLinked);
        Assert.Equal("Jane Doe", result.LinkedInName);
        Assert.NotNull(result.TokenExpiry);
    }

    // ──────────────────────────────────────────────
    // Transaction DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void CreateReferralRequest_Valid_Passes()
    {
        var req = new CreateReferralRequest
        {
            ReceiverUserId = "u-456",
            NetworkId = "net-789"
        };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateReferralRequest_EmptyReceiver_Fails()
    {
        var req = new CreateReferralRequest
        {
            ReceiverUserId = "",
            NetworkId = "net-789"
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateReferralRequest_NotesExceedsMax_Fails()
    {
        var req = new CreateReferralRequest
        {
            ReceiverUserId = "u-456",
            NetworkId = "net-789",
            Notes = new string('a', 1001)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateReferralRequest_SpecialtyExceedsMax_Fails()
    {
        var req = new CreateReferralRequest
        {
            ReceiverUserId = "u-456",
            NetworkId = "net-789",
            Specialty = new string('a', 101)
        };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void RecordOutcomeRequest_Valid_Passes()
    {
        var req = new RecordOutcomeRequest { Outcome = "Patient treated successfully", Success = true };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void RecordOutcomeRequest_EmptyOutcome_Fails()
    {
        var req = new RecordOutcomeRequest { Outcome = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void RecordOutcomeRequest_OutcomeExceedsMax_Fails()
    {
        var req = new RecordOutcomeRequest { Outcome = new string('a', 2001) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void UpdateReferralStatusRequest_Valid_Passes()
    {
        var req = new UpdateReferralStatusRequest { Status = ReferralStatus.Accepted };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateDealRoomRequest_Valid_Passes()
    {
        var req = new CreateDealRoomRequest { Name = "Q1 Acquisition" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void CreateDealRoomRequest_EmptyName_Fails()
    {
        var req = new CreateDealRoomRequest { Name = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void CreateDealRoomRequest_NameExceedsMax_Fails()
    {
        var req = new CreateDealRoomRequest { Name = new string('a', 201) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void AddParticipantRequest_Valid_Passes()
    {
        var req = new AddParticipantRequest { UserId = "u-123", Role = "buyer" };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void AddParticipantRequest_EmptyUserId_Fails()
    {
        var req = new AddParticipantRequest { UserId = "", Role = "buyer" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void AddParticipantRequest_EmptyRole_Fails()
    {
        var req = new AddParticipantRequest { UserId = "u-123", Role = "" };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void DealRoomResponse_RoundTrips()
    {
        var resp = new DealRoomResponse
        {
            DealId = "deal-001",
            Name = "Acquisition",
            CreatedByUserId = "u-123",
            Status = DealStatus.Active,
            ParticipantCount = 3,
            DocumentCount = 5,
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<DealRoomResponse>(json)!;
        Assert.Equal(resp.DealId, result.DealId);
        Assert.Equal(resp.Status, result.Status);
        Assert.Equal(resp.ParticipantCount, result.ParticipantCount);
    }

    [Fact]
    public void DealRoomListResponse_RoundTrips()
    {
        var resp = new DealRoomListResponse
        {
            DealRooms = new List<DealRoomResponse>
            {
                new() { DealId = "deal-001", Name = "Acq" }
            },
            NextToken = "next"
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<DealRoomListResponse>(json)!;
        Assert.Single(result.DealRooms);
        Assert.Equal("next", result.NextToken);
    }

    [Fact]
    public void PresignedUrlResponse_RoundTrips()
    {
        var resp = new PresignedUrlResponse { Url = "https://s3.example.com/signed", ExpiresIn = 3600 };
        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<PresignedUrlResponse>(json)!;
        Assert.Equal(resp.Url, result.Url);
        Assert.Equal(3600, result.ExpiresIn);
    }

    // ──────────────────────────────────────────────
    // Notification DTOs
    // ──────────────────────────────────────────────

    [Fact]
    public void UpdatePreferencesRequest_Valid_Passes()
    {
        var req = new UpdatePreferencesRequest
        {
            DigestTime = "08:30",
            Timezone = "America/Denver"
        };
        Assert.True(IsValid(req));
    }

    [Theory]
    [InlineData("8:30")]
    [InlineData("25:00")]
    [InlineData("12:60")]
    [InlineData("noon")]
    public void UpdatePreferencesRequest_InvalidDigestTime_Fails(string time)
    {
        var req = new UpdatePreferencesRequest { DigestTime = time };
        Assert.False(IsValid(req));
    }

    [Theory]
    [InlineData("00:00")]
    [InlineData("23:59")]
    [InlineData("12:30")]
    [InlineData("07:00")]
    public void UpdatePreferencesRequest_ValidDigestTime_Passes(string time)
    {
        var req = new UpdatePreferencesRequest { DigestTime = time };
        Assert.True(IsValid(req));
    }

    [Fact]
    public void UpdatePreferencesRequest_TimezoneExceedsMax_Fails()
    {
        var req = new UpdatePreferencesRequest { Timezone = new string('a', 51) };
        Assert.False(IsValid(req));
    }

    [Fact]
    public void NotificationListResponse_RoundTrips()
    {
        var resp = new NotificationListResponse
        {
            Notifications = new List<NotificationResponse>
            {
                new() { NotificationId = "n-1", Title = "Test", Body = "Body" }
            },
            UnreadCount = 5,
            NextToken = "tok"
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<NotificationListResponse>(json)!;
        Assert.Single(result.Notifications);
        Assert.Equal(5, result.UnreadCount);
        Assert.Equal("tok", result.NextToken);
    }

    [Fact]
    public void DigestPreviewResponse_RoundTrips()
    {
        var resp = new DigestPreviewResponse
        {
            Categories = new Dictionary<string, int> { { "Referrals", 3 }, { "Network", 2 } },
            TopItems = new List<NotificationResponse>
            {
                new() { NotificationId = "n-1", Title = "Item" }
            }
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<DigestPreviewResponse>(json)!;
        Assert.Equal(2, result.Categories.Count);
        Assert.Single(result.TopItems);
    }

    // ──────────────────────────────────────────────
    // Response DTOs — additional round-trip coverage
    // ──────────────────────────────────────────────

    [Fact]
    public void ReferralResponse_RoundTrips()
    {
        var resp = new ReferralResponse
        {
            ReferralId = "ref-001",
            SenderUserId = "u-123",
            ReceiverUserId = "u-456",
            SenderDisplayName = "Alice",
            ReceiverDisplayName = "Bob",
            NetworkId = "net-789",
            Status = ReferralStatus.Completed,
            OutcomeRecordedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<ReferralResponse>(json)!;
        Assert.Equal(resp.ReferralId, result.ReferralId);
        Assert.Equal(resp.Status, result.Status);
        Assert.Equal(resp.SenderDisplayName, result.SenderDisplayName);
    }

    [Fact]
    public void ReputationResponse_RoundTrips()
    {
        var resp = new ReputationResponse
        {
            UserId = "u-123",
            OverallScore = 87.5m,
            ReferralScore = 92.0m,
            ContributionScore = 78.3m,
            AttestationScore = 95.1m,
            ComputedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<ReputationResponse>(json)!;
        Assert.Equal(resp.OverallScore, result.OverallScore);
    }

    [Fact]
    public void MemberResponse_RoundTrips()
    {
        var resp = new MemberResponse
        {
            UserId = "u-123",
            DisplayName = "Jane",
            Role = "owner",
            JoinedAt = DateTime.UtcNow,
            ContributionScore = 95.0m
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<MemberResponse>(json)!;
        Assert.Equal(resp.UserId, result.UserId);
        Assert.Equal(resp.ContributionScore, result.ContributionScore);
    }

    [Fact]
    public void NetworkResponse_RoundTrips()
    {
        var resp = new NetworkResponse
        {
            NetworkId = "net-456",
            Name = "Denver Dental",
            MemberCount = 42,
            UserRole = "member"
        };

        var json = JsonSerializer.Serialize(resp);
        var result = JsonSerializer.Deserialize<NetworkResponse>(json)!;
        Assert.Equal(resp.NetworkId, result.NetworkId);
        Assert.Equal(resp.MemberCount, result.MemberCount);
        Assert.Equal("member", result.UserRole);
    }
}

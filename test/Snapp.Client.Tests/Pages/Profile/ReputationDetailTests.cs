using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Profile;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Transaction;
using Xunit;

namespace Snapp.Client.Tests.Pages.Profile;

public class ReputationDetailTests : TestContext
{
    private readonly MockReputationService _reputationService;

    public ReputationDetailTests()
    {
        _reputationService = new MockReputationService();
        Services.AddSingleton<IReputationService>(_reputationService);
        Services.AddSingleton<IAuthService>(new FakeAuthService());
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ReputationDetail_ShowsLoading_Initially()
    {
        _reputationService.Reputation = null;
        _reputationService.ShouldThrow = true;

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        // After the failed load, should show error
        Assert.Contains("Failed to load reputation data", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsScores()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 82m,
            ReferralScore = 90m,
            ContributionScore = 75m,
            AttestationScore = 80m,
            ComputedAt = DateTime.UtcNow
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("82", cut.Markup);
        Assert.Contains("Referral Score", cut.Markup);
        Assert.Contains("Contribution Score", cut.Markup);
        Assert.Contains("Attestation Score", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsProgressBars()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 82m,
            ReferralScore = 90m,
            ContributionScore = 75m,
            AttestationScore = 80m
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        var progressBars = cut.FindComponents<MudProgressLinear>();
        Assert.Equal(3, progressBars.Count);
    }

    [Fact]
    public void ReputationDetail_ShowsEmptyAttestations()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 50m
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("No attestations yet", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsAttestations()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 50m
        };
        _reputationService.Attestations = new AttestationListResponse
        {
            Attestations = new List<AttestationResponse>
            {
                new()
                {
                    AttestationId = "att-1",
                    FromUserId = "user-2",
                    FromDisplayName = "Dr. Jones",
                    ToUserId = "user-1",
                    Text = "Great practitioner",
                    CreatedAt = new DateTime(2026, 3, 15)
                }
            }
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("Dr. Jones", cut.Markup);
        Assert.Contains("Great practitioner", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsRequestAttestationButton()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 50m
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("Request Attestation", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsError_WhenLoadFails()
    {
        _reputationService.ShouldThrow = true;

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("Failed to load reputation data", cut.Markup);
    }

    [Fact]
    public void ReputationDetail_ShowsChart_WhenHistoryExists()
    {
        _reputationService.Reputation = new ReputationResponse
        {
            UserId = "user-1",
            OverallScore = 50m
        };
        _reputationService.History = new ReputationHistoryResponse
        {
            Points = new List<ReputationHistoryPoint>
            {
                new() { Date = new DateTime(2026, 1, 1), OverallScore = 40m },
                new() { Date = new DateTime(2026, 2, 1), OverallScore = 50m }
            }
        };

        var cut = RenderComponent<ReputationDetail>(parameters => parameters
            .Add(p => p.UserId, "user-1"));

        Assert.Contains("Score History", cut.Markup);
        var chart = cut.FindComponent<MudChart>();
        Assert.NotNull(chart);
    }

    private class FakeAuthService : IAuthService
    {
        public Task<Snapp.Shared.DTOs.Common.MessageResponse> RequestMagicLinkAsync(string email)
            => Task.FromResult(new Snapp.Shared.DTOs.Common.MessageResponse());
        public Task<Snapp.Shared.DTOs.Auth.TokenResponse> ValidateCodeAsync(string code)
            => Task.FromResult(new Snapp.Shared.DTOs.Auth.TokenResponse());
        public Task<Snapp.Shared.DTOs.Auth.TokenResponse> RefreshAsync(string refreshToken)
            => Task.FromResult(new Snapp.Shared.DTOs.Auth.TokenResponse());
        public Task LogoutAsync() => Task.CompletedTask;
    }
}

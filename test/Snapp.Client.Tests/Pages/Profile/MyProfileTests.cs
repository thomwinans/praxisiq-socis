using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Profile;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.User;
using Xunit;

namespace Snapp.Client.Tests.Pages.Profile;

public class MyProfileTests : TestContext
{
    private readonly FakeSdkWrapper _sdkWrapper = new();

    public MyProfileTests()
    {
        Services.AddSingleton<SnappSdkWrapper>(_sdkWrapper);
        Services.AddSingleton<IReputationService>(new MockReputationService());
        Services.AddSingleton<ILinkedInService>(new MockLinkedInService());
        Services.AddSingleton<IAuthService>(new FakeAuthService());
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void MyProfile_ShowsProfileData()
    {
        _sdkWrapper.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            Specialty = "Orthodontics",
            Geography = "California",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<MyProfile>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Dr. Smith", cut.Markup);
            Assert.Contains("Orthodontics", cut.Markup);
            Assert.Contains("California", cut.Markup);
        });
    }

    [Fact]
    public void MyProfile_ShowsEditButton()
    {
        _sdkWrapper.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<MyProfile>();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindComponents<MudButton>();
            Assert.Contains(buttons, b => b.Markup.Contains("Edit Profile"));
        });
    }

    [Fact]
    public void MyProfile_ShowsCompletenessBar()
    {
        _sdkWrapper.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            ProfileCompleteness = 45
        };

        var cut = RenderComponent<MyProfile>();

        cut.WaitForAssertion(() =>
        {
            var completeness = cut.FindComponent<Snapp.Client.Components.ProfileCompleteness>();
            Assert.NotNull(completeness);
        });
    }

    [Fact]
    public void MyProfile_ShowsCompletenessHint_WhenNotComplete()
    {
        _sdkWrapper.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            ProfileCompleteness = 40
        };

        var cut = RenderComponent<MyProfile>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Info, alert.Instance.Severity);
            Assert.Contains("40%", cut.Markup);
        });
    }

    [Fact]
    public void MyProfile_ShowsError_OnApiFailure()
    {
        _sdkWrapper.ThrowOnGet = true;

        var cut = RenderComponent<MyProfile>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
        });
    }

    private class FakeSdkWrapper : SnappSdkWrapper
    {
        public ProfileResponse ProfileResult { get; set; } = new() { UserId = "user-1", DisplayName = "Test" };
        public bool ThrowOnGet { get; set; }

        public FakeSdkWrapper() : base(null!, null!, null!) { }

        public override Task<ProfileResponse> GetMyProfileAsync()
        {
            if (ThrowOnGet) throw new SnappApiException("Failed");
            return Task.FromResult(ProfileResult);
        }

        public override Task<ProfileResponse> GetProfileAsync(string userId) => Task.FromResult(ProfileResult);
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

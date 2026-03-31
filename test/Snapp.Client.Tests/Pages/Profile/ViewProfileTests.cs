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

public class ViewProfileTests : TestContext
{
    private readonly FakeUserService _userService = new();

    public ViewProfileTests()
    {
        Services.AddSingleton<IUserService>(_userService);
        Services.AddSingleton<IReputationService>(new MockReputationService());
        Services.AddSingleton<IAuthService>(new FakeAuthService());
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ViewProfile_ShowsProfileData()
    {
        _userService.ProfileResult = new ProfileResponse
        {
            UserId = "other-user",
            DisplayName = "Dr. Jones",
            Specialty = "Periodontics",
            Geography = "Texas",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<ViewProfile>(parameters =>
            parameters.Add(p => p.UserId, "other-user"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Dr. Jones", cut.Markup);
            Assert.Contains("Periodontics", cut.Markup);
            Assert.Contains("Texas", cut.Markup);
        });
    }

    [Fact]
    public void ViewProfile_ShowsLoadingIndicator()
    {
        _userService.Delay = TimeSpan.FromSeconds(5);

        var cut = RenderComponent<ViewProfile>(parameters =>
            parameters.Add(p => p.UserId, "other-user"));

        var progress = cut.FindComponent<MudProgressCircular>();
        Assert.NotNull(progress);
    }

    [Fact]
    public void ViewProfile_ShowsError_OnApiFailure()
    {
        _userService.ThrowOnGet = true;

        var cut = RenderComponent<ViewProfile>(parameters =>
            parameters.Add(p => p.UserId, "other-user"));

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
        });
    }

    [Fact]
    public void ViewProfile_ShowsAvatarPlaceholder_WhenNoPhoto()
    {
        _userService.ProfileResult = new ProfileResponse
        {
            UserId = "other-user",
            DisplayName = "Dr. Jones",
            ProfileCompleteness = 40
        };

        var cut = RenderComponent<ViewProfile>(parameters =>
            parameters.Add(p => p.UserId, "other-user"));

        cut.WaitForAssertion(() =>
        {
            var avatar = cut.FindComponent<MudAvatar>();
            Assert.NotNull(avatar);
        });
    }

    private class FakeUserService : IUserService
    {
        public ProfileResponse ProfileResult { get; set; } = new() { UserId = "user-1", DisplayName = "Test" };
        public bool ThrowOnGet { get; set; }
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public async Task<ProfileResponse> GetMyProfileAsync()
        {
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay);
            if (ThrowOnGet) throw new HttpRequestException("Failed");
            return ProfileResult;
        }

        public async Task<ProfileResponse> GetProfileAsync(string userId)
        {
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay);
            if (ThrowOnGet) throw new HttpRequestException("Failed");
            return ProfileResult;
        }

        public Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request) => Task.FromResult(ProfileResult);
        public Task<ProfileResponse> OnboardAsync(OnboardingRequest request) => Task.FromResult(ProfileResult);
        public Task<PiiResponse> GetMyPiiAsync() => Task.FromResult(new PiiResponse());
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

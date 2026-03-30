using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Profile;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Shared.DTOs.User;
using Xunit;

namespace Snapp.Client.Tests.Pages.Profile;

public class EditProfileTests : TestContext
{
    private readonly FakeUserService _userService = new();

    public EditProfileTests()
    {
        Services.AddSingleton<IUserService>(_userService);
        Services.AddSingleton<IAuthService>(new FakeAuthService());
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void EditProfile_LoadsExistingData()
    {
        _userService.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            Specialty = "Orthodontics",
            Geography = "California",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<EditProfile>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Edit Profile", cut.Markup);
            var textFields = cut.FindComponents<MudTextField<string>>();
            var nameField = textFields.First(tf => tf.Instance.Label == "Display Name");
            Assert.Equal("Dr. Smith", nameField.Instance.Value);
            var geoField = textFields.First(tf => tf.Instance.Label == "Geography");
            Assert.Equal("California", geoField.Instance.Value);
        });
    }

    [Fact]
    public void EditProfile_HasSaveAndCancelButtons()
    {
        var cut = RenderComponent<EditProfile>();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindComponents<MudButton>();
            Assert.Contains(buttons, b => b.Markup.Contains("Save"));
            Assert.Contains(buttons, b => b.Markup.Contains("Cancel"));
        });
    }

    [Fact]
    public async Task EditProfile_SaveCallsApi()
    {
        _userService.ProfileResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Dr. Smith",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<EditProfile>();

        cut.WaitForAssertion(() => Assert.Contains("Edit Profile", cut.Markup));

        var buttons = cut.FindComponents<MudButton>();
        var saveButton = buttons.First(b => b.Markup.Contains("Save"));
        await cut.InvokeAsync(() => saveButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.True(_userService.UpdateCalled);
        });
    }

    [Fact]
    public void EditProfile_ShowsError_OnLoadFailure()
    {
        _userService.ThrowOnGet = true;

        var cut = RenderComponent<EditProfile>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
        });
    }

    private class FakeUserService : IUserService
    {
        public ProfileResponse ProfileResult { get; set; } = new() { UserId = "user-1", DisplayName = "Test" };
        public bool ThrowOnGet { get; set; }
        public bool UpdateCalled { get; private set; }

        public Task<ProfileResponse> GetMyProfileAsync()
        {
            if (ThrowOnGet) throw new HttpRequestException("Failed");
            return Task.FromResult(ProfileResult);
        }

        public Task<ProfileResponse> GetProfileAsync(string userId) => Task.FromResult(ProfileResult);

        public Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request)
        {
            UpdateCalled = true;
            return Task.FromResult(ProfileResult);
        }

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

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

public class OnboardingTests : TestContext
{
    private readonly FakeUserService _userService = new();

    public OnboardingTests()
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
    public void Onboarding_RendersStep1ByDefault()
    {
        var cut = RenderComponent<Onboarding>();

        Assert.Contains("About You", cut.Markup);
        var textFields = cut.FindComponents<MudTextField<string>>();
        Assert.Contains(textFields, tf => tf.Instance.Label == "Display Name");
    }

    [Fact]
    public void Onboarding_RendersProgressBar()
    {
        var cut = RenderComponent<Onboarding>();

        var progress = cut.FindComponent<MudProgressLinear>();
        Assert.NotNull(progress);
    }

    [Fact]
    public async Task Onboarding_NextButton_AdvancesToStep2()
    {
        var cut = RenderComponent<Onboarding>();

        // Set display name to make step 1 valid
        var nameField = cut.FindComponents<MudTextField<string>>()
            .First(tf => tf.Instance.Label == "Display Name");
        await cut.InvokeAsync(() => nameField.Instance.SetText("Test User"));

        // Click Next
        var buttons = cut.FindComponents<MudButton>();
        var nextButton = buttons.First(b => b.Markup.Contains("Next"));
        await cut.InvokeAsync(() => nextButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Your Practice", cut.Markup);
        });
    }

    [Fact]
    public async Task Onboarding_Step2HasSkipButton()
    {
        var cut = RenderComponent<Onboarding>();

        // Advance to step 2
        var nameField = cut.FindComponents<MudTextField<string>>()
            .First(tf => tf.Instance.Label == "Display Name");
        await cut.InvokeAsync(() => nameField.Instance.SetText("Test User"));

        var buttons = cut.FindComponents<MudButton>();
        var nextButton = buttons.First(b => b.Markup.Contains("Next"));
        await cut.InvokeAsync(() => nextButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            var allButtons = cut.FindComponents<MudButton>();
            Assert.Contains(allButtons, b => b.Markup.Contains("Skip"));
        });
    }

    [Fact]
    public async Task Onboarding_FinishButton_CallsApi()
    {
        _userService.OnboardResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Test User",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<Onboarding>();

        // Step 1: fill name
        var nameField = cut.FindComponents<MudTextField<string>>()
            .First(tf => tf.Instance.Label == "Display Name");
        await cut.InvokeAsync(() => nameField.Instance.SetText("Test User"));

        // Advance to step 2
        var buttons = cut.FindComponents<MudButton>();
        var nextButton = buttons.First(b => b.Markup.Contains("Next"));
        await cut.InvokeAsync(() => nextButton.Instance.OnClick.InvokeAsync());

        // Advance to step 3
        cut.WaitForAssertion(() => Assert.Contains("Your Practice", cut.Markup));
        buttons = cut.FindComponents<MudButton>();
        nextButton = buttons.First(b => b.Markup.Contains("Next"));
        await cut.InvokeAsync(() => nextButton.Instance.OnClick.InvokeAsync());

        // Click Finish
        cut.WaitForAssertion(() => Assert.Contains("Connect", cut.Markup));
        buttons = cut.FindComponents<MudButton>();
        var finishButton = buttons.First(b => b.Markup.Contains("Finish"));
        await cut.InvokeAsync(() => finishButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.True(_userService.OnboardCalled);
        });
    }

    private class FakeUserService : IUserService
    {
        public ProfileResponse ProfileResult { get; set; } = new() { UserId = "user-1", DisplayName = "Test" };
        public ProfileResponse OnboardResult { get; set; } = new();
        public PiiResponse PiiResult { get; set; } = new() { Email = "test@example.com" };
        public bool OnboardCalled { get; private set; }
        public bool ThrowOnGet { get; set; }

        public Task<ProfileResponse> GetMyProfileAsync()
        {
            if (ThrowOnGet) throw new HttpRequestException("Failed");
            return Task.FromResult(ProfileResult);
        }

        public Task<ProfileResponse> GetProfileAsync(string userId) => Task.FromResult(ProfileResult);

        public Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request) => Task.FromResult(ProfileResult);

        public Task<ProfileResponse> OnboardAsync(OnboardingRequest request)
        {
            OnboardCalled = true;
            return Task.FromResult(OnboardResult);
        }

        public Task<PiiResponse> GetMyPiiAsync() => Task.FromResult(PiiResult);
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

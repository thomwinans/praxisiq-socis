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

public class OnboardingTests : TestContext
{
    private readonly FakeUserService _userService = new();

    public OnboardingTests()
    {
        Services.AddSingleton<IUserService>(_userService);
        Services.AddSingleton<IAuthService>(new FakeAuthService());
        Services.AddSingleton<ILinkedInService>(new MockLinkedInService());
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

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Tell us about yourself", cut.Markup);
            var textFields = cut.FindComponents<MudTextField<string>>();
            Assert.Contains(textFields, tf => tf.Instance.Label == "First name");
            Assert.Contains(textFields, tf => tf.Instance.Label == "Last name");
            Assert.Contains(textFields, tf => tf.Instance.Label == "Display name");
        });
    }

    [Fact]
    public void Onboarding_RendersPageHeading()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Let's set up your profile", cut.Markup);
            Assert.Contains("Takes about 2 minutes", cut.Markup);
        });
    }

    [Fact]
    public void Onboarding_RendersProgressBar()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() =>
        {
            var progress = cut.FindComponent<MudProgressLinear>();
            Assert.NotNull(progress);
        });
    }

    [Fact]
    public async Task Onboarding_ContinueButton_AdvancesToStep2()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() => Assert.Contains("Tell us about yourself", cut.Markup));

        // Set required fields to make step 1 valid
        var textFields = cut.FindComponents<MudTextField<string>>();
        var firstNameField = textFields.First(tf => tf.Instance.Label == "First name");
        var lastNameField = textFields.First(tf => tf.Instance.Label == "Last name");
        await cut.InvokeAsync(() => firstNameField.Instance.SetText("Test"));
        await cut.InvokeAsync(() => lastNameField.Instance.SetText("User"));

        // Click Continue
        var buttons = cut.FindComponents<MudButton>();
        var continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("About your practice", cut.Markup);
        });
    }

    [Fact]
    public async Task Onboarding_Step2HasSkipButton()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() => Assert.Contains("Tell us about yourself", cut.Markup));

        // Advance to step 2
        var textFields = cut.FindComponents<MudTextField<string>>();
        var firstNameField = textFields.First(tf => tf.Instance.Label == "First name");
        await cut.InvokeAsync(() => firstNameField.Instance.SetText("Test"));

        var buttons = cut.FindComponents<MudButton>();
        var continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            var allButtons = cut.FindComponents<MudButton>();
            Assert.Contains(allButtons, b => b.Markup.Contains("Skip"));
        });
    }

    [Fact]
    public async Task Onboarding_Step2HasPracticeFields()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() => Assert.Contains("Tell us about yourself", cut.Markup));

        // Advance to step 2
        var textFields = cut.FindComponents<MudTextField<string>>();
        var firstNameField = textFields.First(tf => tf.Instance.Label == "First name");
        await cut.InvokeAsync(() => firstNameField.Instance.SetText("Test"));

        var buttons = cut.FindComponents<MudButton>();
        var continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("About your practice", cut.Markup);
            Assert.Contains("benchmarking and practice intelligence", cut.Markup);
            var practiceName = cut.FindComponents<MudTextField<string>>();
            Assert.Contains(practiceName, tf => tf.Instance.Label == "Practice name");
        });
    }

    [Fact]
    public async Task Onboarding_Step3HasLinkedInSection()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() => Assert.Contains("Tell us about yourself", cut.Markup));

        // Advance to step 2
        var textFields = cut.FindComponents<MudTextField<string>>();
        var firstNameField = textFields.First(tf => tf.Instance.Label == "First name");
        await cut.InvokeAsync(() => firstNameField.Instance.SetText("Test"));

        var buttons = cut.FindComponents<MudButton>();
        var continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        // Advance to step 3
        cut.WaitForAssertion(() => Assert.Contains("About your practice", cut.Markup));
        buttons = cut.FindComponents<MudButton>();
        continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Connect your accounts", cut.Markup);
            Assert.Contains("Connect your LinkedIn profile", cut.Markup);
            Assert.Contains("credentials are never stored", cut.Markup);
        });
    }

    [Fact]
    public async Task Onboarding_GetStartedButton_CallsApi()
    {
        _userService.OnboardResult = new ProfileResponse
        {
            UserId = "user-1",
            DisplayName = "Test User",
            ProfileCompleteness = 60
        };

        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() => Assert.Contains("Tell us about yourself", cut.Markup));

        // Step 1: fill name
        var textFields = cut.FindComponents<MudTextField<string>>();
        var firstNameField = textFields.First(tf => tf.Instance.Label == "First name");
        await cut.InvokeAsync(() => firstNameField.Instance.SetText("Test"));

        // Advance to step 2
        var buttons = cut.FindComponents<MudButton>();
        var continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        // Advance to step 3
        cut.WaitForAssertion(() => Assert.Contains("About your practice", cut.Markup));
        buttons = cut.FindComponents<MudButton>();
        continueButton = buttons.First(b => b.Markup.Contains("Continue"));
        await cut.InvokeAsync(() => continueButton.Instance.OnClick.InvokeAsync());

        // Click Get Started
        cut.WaitForAssertion(() => Assert.Contains("Connect your accounts", cut.Markup));
        buttons = cut.FindComponents<MudButton>();
        var getStartedButton = buttons.First(b => b.Markup.Contains("Get Started"));
        await cut.InvokeAsync(() => getStartedButton.Instance.OnClick.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.True(_userService.OnboardCalled);
        });
    }

    [Fact]
    public void Onboarding_ShowsBottomHelperText()
    {
        var cut = RenderComponent<Onboarding>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("You can update this anytime in your profile settings", cut.Markup);
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

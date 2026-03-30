using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Auth;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;
using Xunit;

namespace Snapp.Client.Tests.Pages.Auth;

public class LoginTests : TestContext
{
    private readonly FakeAuthService _authService = new();

    public LoginTests()
    {
        Services.AddSingleton<IAuthService>(_authService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Login_RendersEmailFieldAndButton()
    {
        var cut = RenderComponent<Login>();

        var textField = cut.FindComponent<MudTextField<string>>();
        Assert.NotNull(textField);
        Assert.Equal("Email", textField.Instance.Label);

        var button = cut.FindComponent<MudButton>();
        Assert.Contains("Send Login Link", button.Markup);
    }

    [Fact]
    public async Task Login_SubmitValidEmail_ShowsSentState()
    {
        _authService.RequestResult = new MessageResponse { Message = "Sent" };
        var cut = RenderComponent<Login>();

        // Set email value
        var textField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => textField.Instance.SetText("test@example.com"));

        // Find and click the submit button
        var buttons = cut.FindComponents<MudButton>();
        var sendButton = buttons.First(b => b.Markup.Contains("Send Login Link"));
        await cut.InvokeAsync(() => sendButton.Instance.OnClick.InvokeAsync());

        // Should show success alert
        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Success, alert.Instance.Severity);
            Assert.Contains("Check your email", alert.Markup);
        });

        // Should show "Send Again" button
        var sendAgainButtons = cut.FindComponents<MudButton>();
        Assert.Contains(sendAgainButtons, b => b.Markup.Contains("Send Again"));
    }

    private class FakeAuthService : IAuthService
    {
        public MessageResponse RequestResult { get; set; } = new() { Message = "Sent" };
        public TokenResponse ValidateResult { get; set; } = new();
        public TokenResponse RefreshResult { get; set; } = new();
        public bool ThrowOnRequest { get; set; }
        public bool ThrowOnValidate { get; set; }

        public Task<MessageResponse> RequestMagicLinkAsync(string email)
        {
            if (ThrowOnRequest) throw new HttpRequestException("Failed");
            return Task.FromResult(RequestResult);
        }

        public Task<TokenResponse> ValidateCodeAsync(string code)
        {
            if (ThrowOnValidate) throw new HttpRequestException("Failed");
            return Task.FromResult(ValidateResult);
        }

        public Task<TokenResponse> RefreshAsync(string refreshToken)
            => Task.FromResult(RefreshResult);

        public Task LogoutAsync() => Task.CompletedTask;
    }
}

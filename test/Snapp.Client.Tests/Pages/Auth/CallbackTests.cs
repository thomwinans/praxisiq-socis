using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Auth;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;
using Xunit;

namespace Snapp.Client.Tests.Pages.Auth;

public class CallbackTests : TestContext
{
    private readonly FakeAuthService _authService = new();

    public CallbackTests()
    {
        Services.AddSingleton<IAuthService>(_authService);
        // Use real SnappAuthStateProvider with loose JS interop so SetTokensAsync works
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Callback_WithValidCode_RedirectsToHome()
    {
        _authService.ValidateResult = new TokenResponse
        {
            AccessToken = CreateFakeJwt(),
            RefreshToken = "refresh-token",
            IsNewUser = false
        };

        var nav = Services.GetRequiredService<NavigationManager>();
        var uri = nav.GetUriWithQueryParameter("code", "valid-code-12345678901234567890123456789012");
        nav.NavigateTo(uri);
        var cut = RenderComponent<Callback>();

        cut.WaitForAssertion(() =>
        {
            // After successful validation, should navigate to home
            Assert.EndsWith("/", nav.Uri);
        });
    }

    [Fact]
    public void Callback_WithValidCode_NewUser_RedirectsToOnboarding()
    {
        _authService.ValidateResult = new TokenResponse
        {
            AccessToken = CreateFakeJwt(),
            RefreshToken = "refresh-token",
            IsNewUser = true
        };

        var nav = Services.GetRequiredService<NavigationManager>();
        var uri = nav.GetUriWithQueryParameter("code", "valid-code-12345678901234567890123456789012");
        nav.NavigateTo(uri);
        var cut = RenderComponent<Callback>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("/onboarding", nav.Uri);
        });
    }

    [Fact]
    public void Callback_WithInvalidCode_ShowsError()
    {
        _authService.ThrowOnValidate = true;

        var nav = Services.GetRequiredService<NavigationManager>();
        var uri = nav.GetUriWithQueryParameter("code", "invalid-code-1234567890123456789012345678");
        nav.NavigateTo(uri);
        var cut = RenderComponent<Callback>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains("expired or invalid", alert.Markup);
        });

        var buttons = cut.FindComponents<MudButton>();
        Assert.Contains(buttons, b => b.Markup.Contains("Back to Login"));
    }

    [Fact]
    public void Callback_WithNoCode_ShowsError()
    {
        var cut = RenderComponent<Callback>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains("Invalid login link", alert.Markup);
        });
    }

    private static string CreateFakeJwt()
    {
        var header = Convert.ToBase64String("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"u8);
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{{\"sub\":\"user-123\",\"exp\":{exp}}}"));
        var signature = Convert.ToBase64String("fake-signature"u8);
        return $"{header}.{payload}.{signature}";
    }

    private class FakeAuthService : IAuthService
    {
        public MessageResponse RequestResult { get; set; } = new() { Message = "Sent" };
        public TokenResponse ValidateResult { get; set; } = new();
        public TokenResponse RefreshResult { get; set; } = new();
        public bool ThrowOnValidate { get; set; }

        public Task<MessageResponse> RequestMagicLinkAsync(string email)
            => Task.FromResult(RequestResult);

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

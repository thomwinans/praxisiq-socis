using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.Enums;
using Snapp.Shared.Models;
using Xunit;

namespace Snapp.Client.Tests.Pages.Notification;

public class PreferencesTests : TestContext
{
    private readonly FakeNotificationService _notifService = new();

    public PreferencesTests()
    {
        Services.AddSingleton<INotificationService>(_notifService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddTestAuthorization().SetAuthorized("test-user");
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Preferences_RendersForm()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Notification Preferences", cut.Markup);
            Assert.Contains("Daily Digest", cut.Markup);
            Assert.Contains("Immediate Notifications", cut.Markup);
        });
    }

    [Fact]
    public void Preferences_ShowsTimePicker()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            var timePicker = cut.FindComponent<MudTimePicker>();
            Assert.NotNull(timePicker);
            Assert.Equal("Digest Time", timePicker.Instance.Label);
        });
    }

    [Fact]
    public void Preferences_ShowsSwitches()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Referral Received", cut.Markup);
            Assert.Contains("Mentioned in Discussion", cut.Markup);
            Assert.Contains("Application Decision", cut.Markup);
            Assert.Contains("Valuation Changed", cut.Markup);
        });
    }

    [Fact]
    public void Preferences_LoadsExistingPreferences()
    {
        _notifService.PreferencesResult = new NotificationPreferences
        {
            UserId = "user1",
            DigestTime = "09:00",
            Timezone = "America/Chicago",
            ImmediateTypes = new List<NotificationType> { NotificationType.ReferralReceived }
        };

        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Notification Preferences", cut.Markup);
        });
    }

    [Fact]
    public void Preferences_ShowsSaveButton()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Save Preferences", cut.Markup);
        });
    }

    [Fact]
    public async Task Preferences_Save_CallsService()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() => Assert.Contains("Save Preferences", cut.Markup));

        var buttons = cut.FindComponents<MudButton>();
        var saveButton = buttons.First(b => b.Markup.Contains("Save Preferences"));
        await cut.InvokeAsync(() => saveButton.Instance.OnClick.InvokeAsync());

        Assert.True(_notifService.SaveCalled);
    }

    [Fact]
    public void Preferences_ShowsTimezoneAutocomplete()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Notification.Preferences>();

        cut.WaitForAssertion(() =>
        {
            var autocomplete = cut.FindComponent<MudAutocomplete<string>>();
            Assert.NotNull(autocomplete);
            Assert.Equal("Timezone", autocomplete.Instance.Label);
        });
    }

    private class FakeNotificationService : INotificationService
    {
        public NotificationListResponse ListResult { get; set; } = new();
        public NotificationPreferences? PreferencesResult { get; set; }
        public bool SaveCalled { get; private set; }
        public UpdatePreferencesRequest? LastSaveRequest { get; private set; }

        public Task<NotificationListResponse> ListAsync(int limit = 25, string? nextToken = null)
            => Task.FromResult(ListResult);

        public Task<bool> MarkReadAsync(string notificationId) => Task.FromResult(true);
        public Task<bool> MarkAllReadAsync() => Task.FromResult(true);

        public Task<NotificationPreferences?> GetPreferencesAsync()
            => Task.FromResult(PreferencesResult);

        public Task<bool> SavePreferencesAsync(UpdatePreferencesRequest request)
        {
            SaveCalled = true;
            LastSaveRequest = request;
            return Task.FromResult(true);
        }
    }
}

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Layout;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.Enums;
using Snapp.Shared.Models;
using Xunit;

namespace Snapp.Client.Tests.Components.Layout;

public class NotificationDrawerTests : TestContext
{
    private readonly FakeNotificationService _notifService = new();

    public NotificationDrawerTests()
    {
        Services.AddSingleton<INotificationService>(_notifService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Drawer_ShowsEmptyState_WhenNoNotifications()
    {
        _notifService.ListResult = new NotificationListResponse();
        var cut = RenderComponent<NotificationDrawer>(p => p
            .Add(d => d.Open, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No notifications yet", cut.Markup);
        });
    }

    [Fact]
    public void Drawer_ListsNotifications()
    {
        _notifService.ListResult = new NotificationListResponse
        {
            Notifications = new List<NotificationResponse>
            {
                new() { NotificationId = "1", Title = "New Referral", Body = "You got a referral", Type = NotificationType.ReferralReceived, IsRead = false, CreatedAt = DateTime.UtcNow },
                new() { NotificationId = "2", Title = "Mention", Body = "Someone mentioned you", Type = NotificationType.MentionInDiscussion, IsRead = true, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            }
        };

        var cut = RenderComponent<NotificationDrawer>(p => p
            .Add(d => d.Open, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New Referral", cut.Markup);
            Assert.Contains("Mention", cut.Markup);
        });
    }

    [Fact]
    public void Drawer_ShowsDigestPreferencesLink()
    {
        var cut = RenderComponent<NotificationDrawer>(p => p
            .Add(d => d.Open, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Digest Preferences", cut.Markup);
            Assert.Contains("/notifications/preferences", cut.Markup);
        });
    }

    [Fact]
    public async Task Drawer_MarkAllRead_UpdatesNotifications()
    {
        _notifService.ListResult = new NotificationListResponse
        {
            Notifications = new List<NotificationResponse>
            {
                new() { NotificationId = "1", Title = "Test", Body = "Body", Type = NotificationType.ReferralReceived, IsRead = false, CreatedAt = DateTime.UtcNow },
            }
        };

        var readCalled = false;
        var cut = RenderComponent<NotificationDrawer>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.OnNotificationsRead, () => { readCalled = true; }));

        cut.WaitForAssertion(() => Assert.Contains("Test", cut.Markup));

        var buttons = cut.FindComponents<MudButton>();
        var markAllBtn = buttons.FirstOrDefault(b => b.Markup.Contains("Mark All Read"));
        Assert.NotNull(markAllBtn);

        await cut.InvokeAsync(() => markAllBtn!.Instance.OnClick.InvokeAsync());

        Assert.True(readCalled);
        Assert.True(_notifService.MarkAllReadCalled);
    }

    [Fact]
    public void Drawer_ShowsMarkAllReadButton()
    {
        var cut = RenderComponent<NotificationDrawer>(p => p
            .Add(d => d.Open, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mark All Read", cut.Markup);
        });
    }

    private class FakeNotificationService : INotificationService
    {
        public NotificationListResponse ListResult { get; set; } = new();
        public bool MarkAllReadCalled { get; private set; }
        public bool MarkReadCalled { get; private set; }
        public string? LastMarkReadId { get; private set; }

        public Task<NotificationListResponse> ListAsync(int limit = 25, string? nextToken = null)
            => Task.FromResult(ListResult);

        public Task<bool> MarkReadAsync(string notificationId)
        {
            MarkReadCalled = true;
            LastMarkReadId = notificationId;
            return Task.FromResult(true);
        }

        public Task<bool> MarkAllReadAsync()
        {
            MarkAllReadCalled = true;
            return Task.FromResult(true);
        }

        public Task<NotificationPreferences?> GetPreferencesAsync()
            => Task.FromResult<NotificationPreferences?>(null);

        public Task<bool> SavePreferencesAsync(UpdatePreferencesRequest request)
            => Task.FromResult(true);
    }
}

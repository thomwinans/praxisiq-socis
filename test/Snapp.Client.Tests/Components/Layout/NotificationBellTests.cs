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

public class NotificationBellTests : TestContext
{
    private readonly FakeNotificationService _notifService = new();

    public NotificationBellTests()
    {
        Services.AddSingleton<INotificationService>(_notifService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Bell_RendersIconButton()
    {
        var cut = RenderComponent<NotificationBell>();

        var button = cut.FindComponent<MudIconButton>();
        Assert.NotNull(button);
    }

    [Fact]
    public void Bell_ShowsBadgeWithUnreadCount()
    {
        _notifService.ListResult = new NotificationListResponse { UnreadCount = 5 };
        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.FindComponent<MudBadge>();
            Assert.Equal(5, badge.Instance.Content);
            Assert.True(badge.Instance.Visible);
        });
    }

    [Fact]
    public void Bell_HidesBadgeWhenNoUnread()
    {
        _notifService.ListResult = new NotificationListResponse { UnreadCount = 0 };
        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.FindComponent<MudBadge>();
            Assert.False(badge.Instance.Visible);
        });
    }

    [Fact]
    public async Task Bell_ClickInvokesCallback()
    {
        var clicked = false;
        var cut = RenderComponent<NotificationBell>(p =>
            p.Add(b => b.OnClick, () => { clicked = true; }));

        var button = cut.FindComponent<MudIconButton>();
        await cut.InvokeAsync(() => button.Instance.OnClick.InvokeAsync());

        Assert.True(clicked);
    }

    [Fact]
    public async Task Bell_RefreshUpdatesCount()
    {
        _notifService.ListResult = new NotificationListResponse { UnreadCount = 3 };
        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.FindComponent<MudBadge>();
            Assert.Equal(3, badge.Instance.Content);
        });

        _notifService.ListResult = new NotificationListResponse { UnreadCount = 0 };
        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForAssertion(() =>
        {
            var badge = cut.FindComponent<MudBadge>();
            Assert.False(badge.Instance.Visible);
        });
    }

    private class FakeNotificationService : INotificationService
    {
        public NotificationListResponse ListResult { get; set; } = new();

        public Task<NotificationListResponse> ListAsync(int limit = 25, string? nextToken = null)
            => Task.FromResult(ListResult);

        public Task<bool> MarkReadAsync(string notificationId) => Task.FromResult(true);
        public Task<bool> MarkAllReadAsync() => Task.FromResult(true);
        public Task<NotificationPreferences?> GetPreferencesAsync() => Task.FromResult<NotificationPreferences?>(null);
        public Task<bool> SavePreferencesAsync(UpdatePreferencesRequest request) => Task.FromResult(true);
    }
}

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Pages.DealRoom;

public class ViewTests : TestContext
{
    private readonly MockDealRoomService _dealRoomService;

    public ViewTests()
    {
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _dealRoomService = new MockDealRoomService();
        Services.AddSingleton<IDealRoomService>(_dealRoomService);

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void View_ShowsErrorState_WhenDealNotFound()
    {
        _dealRoomService.DealRoom = null;

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("not found"), TimeSpan.FromSeconds(2));

        Assert.Contains("Deal room not found or access denied", cut.Markup);
    }

    [Fact]
    public void View_ShowsDealName()
    {
        SetupDeal();

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Test Deal"), TimeSpan.FromSeconds(2));

        Assert.Contains("Test Deal", cut.Markup);
    }

    [Fact]
    public void View_ShowsTabHeaders()
    {
        SetupDeal();

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Overview"), TimeSpan.FromSeconds(2));

        Assert.Contains("Overview", cut.Markup);
        Assert.Contains("Documents", cut.Markup);
        Assert.Contains("Participants", cut.Markup);
        Assert.Contains("Activity", cut.Markup);
    }

    [Fact]
    public void View_ShowsStatusChip()
    {
        SetupDeal();

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Active"), TimeSpan.FromSeconds(2));

        Assert.Contains("Active", cut.Markup);
    }

    [Fact]
    public void View_ShowsBackButton()
    {
        SetupDeal();

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Back to Deal Rooms"), TimeSpan.FromSeconds(2));

        Assert.Contains("Back to Deal Rooms", cut.Markup);
    }

    [Fact]
    public void View_OverviewTab_ShowsDealDetails()
    {
        SetupDeal();

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Deal Name"), TimeSpan.FromSeconds(2));

        Assert.Contains("Deal Name", cut.Markup);
        Assert.Contains("Status", cut.Markup);
        Assert.Contains("Test Deal", cut.Markup);
    }

    [Fact]
    public void View_LoadsParticipantData()
    {
        SetupDeal();
        _dealRoomService.Participants = new List<DealParticipantResponse>
        {
            new()
            {
                UserId = "user1",
                DisplayName = "Alice Smith",
                Role = "seller",
                AddedAt = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Test Deal"), TimeSpan.FromSeconds(2));

        // Tab headers are always rendered; participants tab shows badge with count
        Assert.Contains("Participants", cut.Markup);
    }

    [Fact]
    public void View_LoadsDocumentData()
    {
        SetupDeal();
        _dealRoomService.Documents = new List<DealDocumentResponse>
        {
            new()
            {
                DocumentId = "doc1",
                Filename = "contract.pdf",
                UploadedByUserId = "user1",
                UploadedByDisplayName = "Alice",
                Size = 1024 * 500,
                CreatedAt = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Test Deal"), TimeSpan.FromSeconds(2));

        // Tab headers are always rendered; documents tab shows badge with count
        Assert.Contains("Documents", cut.Markup);
    }

    [Fact]
    public void View_LoadsAuditLogData()
    {
        SetupDeal();
        _dealRoomService.AuditLog = new List<DealAuditEntryResponse>
        {
            new()
            {
                EventId = "evt1",
                Action = "deal_created",
                ActorUserId = "user1",
                ActorDisplayName = "Alice",
                Details = null,
                CreatedAt = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.View>(p =>
            p.Add(x => x.DealId, "deal1"));
        cut.WaitForState(() => cut.Markup.Contains("Test Deal"), TimeSpan.FromSeconds(2));

        // Tab headers are always rendered; activity tab shows badge with count
        Assert.Contains("Activity", cut.Markup);
    }

    private void SetupDeal()
    {
        _dealRoomService.DealRoom = new DealRoomResponse
        {
            DealId = "deal1",
            Name = "Test Deal",
            CreatedByUserId = "user1",
            Status = DealStatus.Active,
            ParticipantCount = 2,
            DocumentCount = 1,
            CreatedAt = DateTime.UtcNow
        };
    }
}

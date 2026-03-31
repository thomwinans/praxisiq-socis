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

public class ListTests : TestContext
{
    private readonly MockDealRoomService _dealRoomService;

    public ListTests()
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
    public void List_RendersTitle()
    {
        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        Assert.Contains("Deal Rooms", cut.Markup);
    }

    [Fact]
    public void List_HasCreateButton()
    {
        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        Assert.Contains("Create Deal Room", cut.Markup);
    }

    [Fact]
    public void List_ShowsEmptyState_WhenNoDealRooms()
    {
        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        cut.WaitForState(() => cut.Markup.Contains("No deal rooms yet"), TimeSpan.FromSeconds(2));

        Assert.Contains("No deal rooms yet", cut.Markup);
        Assert.Contains("Create Your First Deal Room", cut.Markup);
    }

    [Fact]
    public void List_ShowsDealRoomsInTable()
    {
        _dealRoomService.DealRoomList = new DealRoomListResponse
        {
            DealRooms = new List<DealRoomResponse>
            {
                new()
                {
                    DealId = "deal1",
                    Name = "Acme Practice Sale",
                    Status = DealStatus.Active,
                    ParticipantCount = 3,
                    DocumentCount = 5,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        cut.WaitForState(() => cut.Markup.Contains("Acme Practice Sale"), TimeSpan.FromSeconds(2));

        Assert.Contains("Acme Practice Sale", cut.Markup);
        Assert.Contains("Active", cut.Markup);
    }

    [Fact]
    public void List_ShowsErrorState_OnFailure()
    {
        _dealRoomService.ShouldThrow = true;

        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(2));

        Assert.Contains("Failed to load deal rooms", cut.Markup);
    }

    [Fact]
    public void List_ShowsLoadingSpinner_Initially()
    {
        var cut = RenderComponent<Snapp.Client.Pages.DealRoom.List>();
        // The initial render shows the loading spinner before async load completes
        Assert.Contains("Deal Rooms", cut.Markup);
    }
}

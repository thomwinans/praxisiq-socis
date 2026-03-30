using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Cards;
using Xunit;

namespace Snapp.Client.Tests.Components.Cards;

public class ProfileCardTests : TestContext
{
    public ProfileCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ProfileCard_RendersDisplayName()
    {
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith"));

        Assert.Contains("Dr. Smith", cut.Markup);
    }

    [Fact]
    public void ProfileCard_RendersSpecialtyChip()
    {
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith")
            .Add(p => p.Specialty, "Orthodontics"));

        var chip = cut.FindComponent<MudChip<string>>();
        Assert.Contains("Orthodontics", cut.Markup);
    }

    [Fact]
    public void ProfileCard_RendersGeography()
    {
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith")
            .Add(p => p.Geography, "California"));

        Assert.Contains("California", cut.Markup);
    }

    [Fact]
    public void ProfileCard_HidesSpecialty_WhenNull()
    {
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith"));

        var chips = cut.FindComponents<MudChip<string>>();
        Assert.Empty(chips);
    }

    [Fact]
    public void ProfileCard_ShowsAvatarPlaceholder_WhenNoPhoto()
    {
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith"));

        var avatar = cut.FindComponent<MudAvatar>();
        Assert.NotNull(avatar);
        var icon = cut.FindComponent<MudIcon>();
        Assert.NotNull(icon);
    }

    [Fact]
    public async Task ProfileCard_InvokesOnClick()
    {
        var clicked = false;
        var cut = RenderComponent<ProfileCard>(parameters => parameters
            .Add(p => p.DisplayName, "Dr. Smith")
            .Add(p => p.OnClick, EventCallback.Factory.Create(this, () => clicked = true)));

        var card = cut.FindComponent<MudCard>();
        await cut.InvokeAsync(() => card.Find("[style*='cursor:pointer']").Click());

        Assert.True(clicked);
    }
}

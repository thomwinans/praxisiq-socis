using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Feed;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Components.Feed;

public class PostCardTests : TestContext
{
    public PostCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void PostCard_RendersAuthorAndContent()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Alice",
            Content = "Hello world",
            PostType = PostType.Text,
            ReactionCounts = new(),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void PostCard_MilestonePost_ShowsChip()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Bob",
            Content = "Big milestone",
            PostType = PostType.Milestone,
            ReactionCounts = new(),
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        Assert.Contains("Milestone", cut.Markup);
    }

    [Fact]
    public void PostCard_WithReactions_ShowsCounts()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Alice",
            Content = "Popular post",
            PostType = PostType.Text,
            ReactionCounts = new() { ["like"] = 5, ["insightful"] = 2 },
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        Assert.Contains("5", cut.Markup);
        Assert.Contains("2", cut.Markup);
    }

    [Fact]
    public void PostCard_RendersReactionButtons()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Alice",
            Content = "Content",
            PostType = PostType.Text,
            ReactionCounts = new(),
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        var iconButtons = cut.FindComponents<MudIconButton>();
        Assert.True(iconButtons.Count >= 3, "Should have at least 3 reaction buttons (like, insightful, support)");
    }

    [Fact]
    public void PostCard_RendersMarkdownContent()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Alice",
            Content = "**bold text**",
            PostType = PostType.Text,
            ReactionCounts = new(),
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        Assert.Contains("<strong>bold text</strong>", cut.Markup);
    }

    [Fact]
    public void PostCard_ShowsRelativeTimestamp()
    {
        var post = new PostResponse
        {
            PostId = "p1",
            AuthorUserId = "u1",
            AuthorDisplayName = "Alice",
            Content = "Content",
            PostType = PostType.Text,
            ReactionCounts = new(),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
        };

        var cut = RenderComponent<PostCard>(p =>
            p.Add(x => x.Post, post));

        Assert.Contains("2h ago", cut.Markup);
    }
}

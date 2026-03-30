using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Discussion;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Shared.DTOs.Content;
using Xunit;

namespace Snapp.Client.Tests.Pages.Discussion;

public class ViewTests : TestContext
{
    private readonly FakeDiscussionService _discussionService = new();

    public ViewTests()
    {
        Services.AddSingleton<IDiscussionService>(_discussionService);
        Services.AddScoped<SnappAuthStateProvider>();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void View_Loading_ShowsSpinner()
    {
        _discussionService.DelayMs = 5000;
        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        Assert.NotNull(cut.FindComponent<MudProgressCircular>());
    }

    [Fact]
    public void View_Error_ShowsRetryButton()
    {
        _discussionService.ThrowOnGetThread = true;
        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load thread", cut.Markup);
            Assert.Contains("Retry", cut.Markup);
        });
    }

    [Fact]
    public void View_ThreadNotFound_ShowsError()
    {
        _discussionService.ThreadResult = null;
        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load thread", cut.Markup);
        });
    }

    [Fact]
    public void View_WithThread_RendersTitle()
    {
        _discussionService.ThreadResult = new ThreadResponse
        {
            ThreadId = "t1",
            NetworkId = "net1",
            Title = "Test Discussion",
            AuthorDisplayName = "Alice",
            ReplyCount = 2,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Test Discussion", cut.Markup);
            Assert.Contains("Alice", cut.Markup);
        });
    }

    [Fact]
    public void View_WithReplies_RendersReplyContent()
    {
        _discussionService.ThreadResult = new ThreadResponse
        {
            ThreadId = "t1",
            Title = "Test Thread",
            AuthorDisplayName = "Alice",
            CreatedAt = DateTime.UtcNow,
        };
        _discussionService.ReplyListResult = new ReplyListResponse
        {
            Replies = new List<ReplyResponse>
            {
                new()
                {
                    ReplyId = "r1",
                    ThreadId = "t1",
                    AuthorUserId = "user1",
                    AuthorDisplayName = "Alice",
                    Content = "Hello world",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                },
                new()
                {
                    ReplyId = "r2",
                    ThreadId = "t1",
                    AuthorUserId = "user2",
                    AuthorDisplayName = "Bob",
                    Content = "Great thread!",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                },
            },
        };

        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Hello world", cut.Markup);
            Assert.Contains("Great thread!", cut.Markup);
            Assert.Contains("Alice", cut.Markup);
            Assert.Contains("Bob", cut.Markup);
        });
    }

    [Fact]
    public void View_BackButton_Rendered()
    {
        _discussionService.ThreadResult = new ThreadResponse
        {
            ThreadId = "t1",
            Title = "Test",
            AuthorDisplayName = "A",
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        Assert.Contains("Back to Discussions", cut.Markup);
    }

    [Fact]
    public void View_ReplyComposer_Rendered()
    {
        _discussionService.ThreadResult = new ThreadResponse
        {
            ThreadId = "t1",
            Title = "Test",
            AuthorDisplayName = "A",
            CreatedAt = DateTime.UtcNow,
        };

        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Write a reply", cut.Markup);
            Assert.Contains("Reply", cut.Markup);
        });
    }

    [Fact]
    public void View_MarkdownRendered()
    {
        _discussionService.ThreadResult = new ThreadResponse
        {
            ThreadId = "t1",
            Title = "Test",
            AuthorDisplayName = "A",
            CreatedAt = DateTime.UtcNow,
        };
        _discussionService.ReplyListResult = new ReplyListResponse
        {
            Replies = new List<ReplyResponse>
            {
                new()
                {
                    ReplyId = "r1",
                    ThreadId = "t1",
                    AuthorUserId = "user1",
                    AuthorDisplayName = "Alice",
                    Content = "**bold text**",
                    CreatedAt = DateTime.UtcNow,
                },
            },
        };

        var cut = RenderComponent<View>(p =>
        {
            p.Add(x => x.NetId, "net1");
            p.Add(x => x.ThreadId, "t1");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("<strong>bold text</strong>", cut.Markup);
        });
    }
}

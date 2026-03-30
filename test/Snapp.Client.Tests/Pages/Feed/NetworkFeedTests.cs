using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Feed;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Content;
using Snapp.Shared.DTOs.Network;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Pages.Feed;

public class NetworkFeedTests : TestContext
{
    private readonly FakeFeedService _feedService = new();
    private readonly FakeNetworkServiceForFeed _networkService = new();

    public NetworkFeedTests()
    {
        Services.AddSingleton<IFeedService>(_feedService);
        Services.AddSingleton<INetworkService>(_networkService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Feed_Loading_ShowsSpinner()
    {
        _feedService.DelayMs = 5000;
        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));
        Assert.NotNull(cut.FindComponent<MudProgressCircular>());
    }

    [Fact]
    public void Feed_Empty_ShowsEmptyState()
    {
        _feedService.FeedResult = new FeedResponse { Posts = new() };
        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No posts yet", cut.Markup);
        });
    }

    [Fact]
    public void Feed_WithPosts_RendersPostCards()
    {
        _feedService.FeedResult = new FeedResponse
        {
            Posts = new List<PostResponse>
            {
                new()
                {
                    PostId = "p1",
                    NetworkId = "net1",
                    AuthorUserId = "u1",
                    AuthorDisplayName = "Alice",
                    Content = "Hello world",
                    PostType = PostType.Text,
                    ReactionCounts = new(),
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                },
                new()
                {
                    PostId = "p2",
                    NetworkId = "net1",
                    AuthorUserId = "u2",
                    AuthorDisplayName = "Bob",
                    Content = "Great milestone!",
                    PostType = PostType.Milestone,
                    ReactionCounts = new() { ["like"] = 3 },
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                },
            },
        };

        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Alice", cut.Markup);
            Assert.Contains("Bob", cut.Markup);
            Assert.Contains("Hello world", cut.Markup);
            Assert.Contains("Great milestone!", cut.Markup);
        });
    }

    [Fact]
    public void Feed_WithNextToken_ShowsLoadMoreButton()
    {
        _feedService.FeedResult = new FeedResponse
        {
            Posts = new List<PostResponse>
            {
                new()
                {
                    PostId = "p1",
                    AuthorDisplayName = "A",
                    Content = "Post",
                    ReactionCounts = new(),
                    CreatedAt = DateTime.UtcNow,
                },
            },
            NextToken = "token123",
        };

        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Load More", cut.Markup);
        });
    }

    [Fact]
    public void Feed_Error_ShowsRetryButton()
    {
        _feedService.ThrowOnGetFeed = true;
        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load feed", cut.Markup);
            Assert.Contains("Retry", cut.Markup);
        });
    }

    [Fact]
    public void Feed_ShowsComposer()
    {
        _feedService.FeedResult = new FeedResponse { Posts = new() };
        _networkService.NetworkResult = new NetworkResponse { Name = "Test Network" };
        var cut = RenderComponent<NetworkFeed>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Posting to:", cut.Markup);
            Assert.Contains("Test Network", cut.Markup);
        });
    }
}

internal class FakeFeedService : IFeedService
{
    public FeedResponse FeedResult { get; set; } = new() { Posts = new() };
    public PostResponse? CreatePostResult { get; set; }
    public bool ReactResult { get; set; } = true;
    public bool ThrowOnGetFeed { get; set; }
    public int DelayMs { get; set; }

    public async Task<FeedResponse> GetFeedAsync(string networkId, string? nextToken = null, int limit = 25)
    {
        if (DelayMs > 0) await Task.Delay(DelayMs);
        if (ThrowOnGetFeed) throw new HttpRequestException("Failed");
        return FeedResult;
    }

    public Task<PostResponse?> CreatePostAsync(string networkId, CreatePostRequest request)
        => Task.FromResult(CreatePostResult);

    public Task<bool> ReactAsync(string postId, string reactionType)
        => Task.FromResult(ReactResult);

    public Task<bool> RemoveReactionAsync(string postId, string reactionType)
        => Task.FromResult(ReactResult);
}

internal class FakeNetworkServiceForFeed : INetworkService
{
    public NetworkResponse? NetworkResult { get; set; } = new() { Name = "Test Network" };

    public Task<NetworkListResponse> GetMyNetworksAsync()
        => Task.FromResult(new NetworkListResponse());

    public Task<NetworkListResponse> GetAllNetworksAsync()
        => Task.FromResult(new NetworkListResponse());

    public Task<NetworkResponse?> GetNetworkAsync(string networkId)
        => Task.FromResult(NetworkResult);

    public Task<NetworkResponse?> CreateNetworkAsync(CreateNetworkRequest request)
        => Task.FromResult<NetworkResponse?>(null);

    public Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request)
        => Task.FromResult(true);

    public Task<MemberListResponse> GetMembersAsync(string networkId)
        => Task.FromResult(new MemberListResponse());

    public Task<bool> ApplyAsync(string networkId, string? applicationText)
        => Task.FromResult(true);

    public Task<List<ApplicationResponse>> GetApplicationsAsync(string networkId)
        => Task.FromResult(new List<ApplicationResponse>());

    public Task<bool> DecideApplicationAsync(string networkId, string userId, string decision, string? reason = null)
        => Task.FromResult(true);

    public Task<NetworkSettingsResponse?> GetSettingsAsync(string networkId)
        => Task.FromResult<NetworkSettingsResponse?>(null);

    public Task<bool> RemoveMemberAsync(string networkId, string userId)
        => Task.FromResult(true);

    public Task<bool> ChangeMemberRoleAsync(string networkId, string userId, string newRole)
        => Task.FromResult(true);
}

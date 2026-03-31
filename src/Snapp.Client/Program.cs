using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Snapp.Client;
using Snapp.Client.Handlers;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Sdk.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:8000/api";

// State
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<NetworkState>();
builder.Services.AddScoped<SnappAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SnappAuthStateProvider>());
builder.Services.AddAuthorizationCore();

// HTTP
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddHttpClient("SnappApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient("SnappSdk");

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SnappApi"));

// Kiota SDK
builder.Services.AddSnappSdkScoped((sp, options) =>
{
    options.BaseUrl = apiBaseUrl.TrimEnd('/').Replace("/api", "");
    var authState = sp.GetRequiredService<SnappAuthStateProvider>();
    options.TokenProvider = async () => await authState.GetAccessTokenAsync() ?? string.Empty;
});

builder.Services.AddScoped<SnappSdkWrapper>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INetworkService, NetworkService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDiscussionService, DiscussionService>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IIntelligenceService, IntelligenceService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IReputationService, ReputationService>();
builder.Services.AddScoped<ILinkedInService, LinkedInService>();
builder.Services.AddScoped<IDealRoomService, DealRoomService>();

// MudBlazor
builder.Services.AddMudServices();

await builder.Build().RunAsync();

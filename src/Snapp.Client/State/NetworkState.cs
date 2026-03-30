using Microsoft.JSInterop;

namespace Snapp.Client.State;

public class NetworkState
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "snapp_current_network";

    public NetworkState(IJSRuntime js)
    {
        _js = js;
    }

    public string? CurrentNetworkId { get; private set; }
    public string? CurrentNetworkName { get; private set; }
    public string? UserRole { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        try
        {
            CurrentNetworkId = await _js.InvokeAsync<string?>("authInterop.getToken", StorageKey);
        }
        catch (InvalidOperationException)
        {
            // JS not available during prerender
        }
    }

    public async Task SetCurrentNetworkAsync(string networkId, string networkName, string? userRole = null)
    {
        CurrentNetworkId = networkId;
        CurrentNetworkName = networkName;
        UserRole = userRole;
        await _js.InvokeVoidAsync("authInterop.setToken", StorageKey, networkId);
        OnChange?.Invoke();
    }

    public async Task ClearAsync()
    {
        CurrentNetworkId = null;
        CurrentNetworkName = null;
        UserRole = null;
        await _js.InvokeVoidAsync("authInterop.removeToken", StorageKey);
        OnChange?.Invoke();
    }
}

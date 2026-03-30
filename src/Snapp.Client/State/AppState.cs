namespace Snapp.Client.State;

public class AppState
{
    public bool IsDrawerOpen { get; set; } = true;

    public event Action? OnChange;

    public void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
        OnChange?.Invoke();
    }
}

using Microsoft.JSInterop;

namespace CircleOfTruthAndLove.Services;

/// <summary>
/// Bridges the browser service worker update lifecycle to the Blazor UI.
/// Fires <see cref="OnUpdateAvailable"/> when a new service worker is installed
/// and waiting to take control.
/// </summary>
public class AppUpdateService : IAsyncDisposable
{
    private readonly IJSRuntime js;
    private DotNetObjectReference<AppUpdateService>? selfRef;
    private bool registered;

    public AppUpdateService(IJSRuntime js) => this.js = js;

    public bool UpdateAvailable { get; private set; }

    public event Action? OnUpdateAvailable;

    public async Task RegisterAsync()
    {
        if (registered) return;
        registered = true;
        selfRef = DotNetObjectReference.Create(this);
        try
        {
            await js.InvokeVoidAsync("appUpdate.register", selfRef);
        }
        catch
        {
            // appUpdate.js may be missing in dev builds; ignore.
        }
    }

    /// <summary>Asks the browser to check for a newer service worker now.</summary>
    public async Task CheckForUpdateAsync()
    {
        try { await js.InvokeVoidAsync("appUpdate.checkForUpdate"); }
        catch { }
    }

    /// <summary>Activates the waiting service worker; the page will reload.</summary>
    public async Task ApplyUpdateAsync()
    {
        try { await js.InvokeVoidAsync("appUpdate.apply"); }
        catch { }
    }

    [JSInvokable("OnUpdateAvailable")]
    public void OnUpdateAvailableInvoked()
    {
        UpdateAvailable = true;
        OnUpdateAvailable?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        selfRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}

using Microsoft.JSInterop;

namespace PharmaERP.FieldApp.UI.Services.Device;

public interface INetworkStatus
{
    bool IsOnline { get; }
    event Action<bool>? Changed;
    Task StartAsync();
}

/// <summary>Browser implementation of the device abstractions (plan 4.1). A MAUI host would register
/// native implementations of the same interfaces.</summary>
public sealed class BrowserDevice(IJSRuntime js) : INetworkStatus, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./_content/PharmaERP.FieldApp.UI/js/device.js").AsTask());

    private DotNetObjectReference<BrowserDevice>? _ref;
    private Task? _started;

    public bool IsOnline { get; private set; } = true;
    public event Action<bool>? Changed;
    public event Action? UpdateAvailable;
    public event Action<bool>? VisibilityChanged;

    public Task StartAsync() => _started ??= StartCoreAsync();

    private async Task StartCoreAsync()
    {
        var module = await _module.Value;
        _ref = DotNetObjectReference.Create(this);
        IsOnline = await module.InvokeAsync<bool>("watchNetwork", _ref);
        Changed?.Invoke(IsOnline);
        await module.InvokeVoidAsync("watchVisibility", _ref);
        await module.InvokeVoidAsync("watchForUpdates", _ref);
    }

    public async Task ApplyUpdateAsync() => await (await _module.Value).InvokeVoidAsync("applyUpdate");

    public async Task<DeviceDiagnostics> GetDiagnosticsAsync() =>
        await (await _module.Value).InvokeAsync<DeviceDiagnostics>("getDiagnostics");

    [JSInvokable]
    public void OnNetworkChanged(bool isOnline)
    {
        IsOnline = isOnline;
        Changed?.Invoke(isOnline);
    }

    [JSInvokable]
    public void OnUpdateAvailable() => UpdateAvailable?.Invoke();

    [JSInvokable]
    public void OnVisibilityChanged(bool visible) => VisibilityChanged?.Invoke(visible);

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var module = await _module.Value;
            await module.InvokeVoidAsync("unwatchNetwork");
            await module.DisposeAsync();
        }
        _ref?.Dispose();
    }
}

public sealed record DeviceDiagnostics(
    string UserAgent,
    bool Online,
    bool Standalone,
    string ServiceWorker,
    string GeolocationPermission,
    bool? StoragePersisted,
    long? StorageUsageBytes,
    long? StorageQuotaBytes);

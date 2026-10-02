using PharmaERP.Application.Location;
using PharmaERP.FieldApp.UI.Services.Device;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Location;

public sealed class KvTrackerStorage(KeyValueStore storage) : ITrackerStorage
{
    private const string ConsentKey = "gps:consent";
    private const string BufferKey = "gps:buffer";

    public async Task<bool> GetConsentAsync() => await storage.GetAsync<bool?>(ConsentKey) ?? false;
    public Task SetConsentAsync(bool granted) => storage.SetAsync(ConsentKey, granted);
    public async Task<List<LocationPingRequest>> GetBufferAsync() => await storage.GetAsync<List<LocationPingRequest>>(BufferKey) ?? [];
    public Task SetBufferAsync(List<LocationPingRequest> points) => storage.SetAsync(BufferKey, points);
}

/// <summary>Feeds the tracker its clock and lifecycle: a 30-second tick (working-hours boundaries,
/// upload interval) and foreground/background changes. Kept apart from <see cref="LocationTracker"/> so
/// the tracker itself has no timers and can be tested deterministically.</summary>
public sealed class LocationTrackerRunner(LocationTracker tracker, BrowserDevice device, TimeProvider time) : IAsyncDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public async Task StartAsync()
    {
        if (_loop is not null) return;
        device.VisibilityChanged += OnVisibilityChanged;
        await tracker.StartAsync();
        _cts = new CancellationTokenSource();
        _loop = TickLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_loop is null) return;
        device.VisibilityChanged -= OnVisibilityChanged;
        _cts!.Cancel();
        try { await _loop; } catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _loop = null;
        await tracker.StopAsync();
    }

    private void OnVisibilityChanged(bool visible) => _ = tracker.OnVisibilityChangedAsync(visible);

    private async Task TickLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TickInterval, time);
        while (await timer.WaitForNextTickAsync(ct))
            await tracker.TickAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

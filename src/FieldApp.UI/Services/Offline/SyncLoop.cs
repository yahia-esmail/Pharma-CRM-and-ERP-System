using Microsoft.Extensions.Logging;
using PharmaERP.FieldApp.UI.Services.Device;

namespace PharmaERP.FieldApp.UI.Services.Offline;

/// <summary>Decides <i>when</i> the app syncs (plan 8.2): at start, whenever work is queued, when the
/// network comes back, when the app returns to the foreground, at the next scheduled retry, and every
/// minute as a safety net. Each pass pushes the outbox first, then refreshes stale master data, so what
/// is pulled down already reflects the rep's own changes. It only runs while the app is open — iOS has
/// no Background Sync, and on Android it is deliberately out of scope (see the plan review).</summary>
public sealed class SyncLoop(
    OutboxProcessor processor,
    Outbox outbox,
    MasterDataSync masterData,
    BrowserDevice device,
    TimeProvider time,
    ILogger<SyncLoop> logger) : IAsyncDisposable
{
    private static readonly TimeSpan SafetyInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MinimumWait = TimeSpan.FromSeconds(1);

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private TaskCompletionSource _wake = NewSignal();

    public OutboxRunResult? LastRun { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }

    public void Start()
    {
        if (_loop is not null) return;
        outbox.WorkQueued += Wake;
        device.Changed += OnNetworkChanged;
        device.VisibilityChanged += OnVisibilityChanged;
        _cts = new CancellationTokenSource();
        _loop = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_loop is null) return;
        outbox.WorkQueued -= Wake;
        device.Changed -= OnNetworkChanged;
        device.VisibilityChanged -= OnVisibilityChanged;
        _cts!.Cancel();
        try { await _loop; } catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Wake() => _wake.TrySetResult();

    private void OnNetworkChanged(bool online)
    {
        if (online) Wake();
    }

    private void OnVisibilityChanged(bool visible)
    {
        if (visible) Wake();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // Let the first render finish before touching the network.
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            // Armed before the run, so a Wake() that arrives mid-run triggers another pass right away.
            var signal = _wake = NewSignal();
            var wait = SafetyInterval;
            try
            {
                if (device.IsOnline)
                {
                    LastRun = await processor.RunOnceAsync(ct);
                    LastRunAtUtc = time.GetUtcNow().UtcDateTime;
                    if (!LastRun.StoppedForConnectivity)
                        await masterData.SyncAsync(ct: ct);
                    if (LastRun.NextDueUtc is { } due)
                    {
                        var untilDue = due - time.GetUtcNow().UtcDateTime;
                        wait = untilDue < MinimumWait ? MinimumWait : untilDue < wait ? untilDue : wait;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad run kill the loop — that would silently stop all syncing.
                logger.LogError(ex, "Outbox run failed");
            }

            await Task.WhenAny(signal.Task, Task.Delay(wait, time, ct));
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async ValueTask DisposeAsync() => await StopAsync();
}

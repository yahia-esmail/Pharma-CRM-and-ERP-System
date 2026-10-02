using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace PharmaERP.FieldApp.UI.Services.Location;

/// <summary>Device location, behind an interface (plan 4.1): the PWA uses the browser Geolocation API; a
/// future MAUI host can register a native implementation with background tracking.</summary>
public interface ILocationService
{
    Task<GeoPermission> GetPermissionAsync();

    /// <summary>Best-of-N fix for an event. <paramref name="accuracyProgress"/> reports the best accuracy
    /// so far, for a live "Locating… 45 m → 18 m" indicator.</summary>
    Task<GeoFixResult> GetBestFixAsync(double targetAccuracyMeters, int maxWaitMs,
        IProgress<double>? accuracyProgress = null, CancellationToken ct = default);

    /// <summary>Ends a running <see cref="GetBestFixAsync"/> early with the best reading so far.</summary>
    Task AcceptCurrentFixAsync();

    Task<GeoError> StartWatchAsync(GeoFixSource source);
    Task StopWatchAsync();

    /// <summary>Keeps the screen on (Route Mode). Returns false where unsupported.</summary>
    Task<bool> SetWakeLockAsync(bool enabled);

    Task<double?> GetBatteryLevelAsync();

    Task<string> GetPlatformAsync();

    event Action<GeoFix>? FixReceived;
    event Action<GeoError, string>? WatchError;
}

public sealed class BrowserLocationService(IJSRuntime js) : ILocationService, IAsyncDisposable
{
    // Imported on first use and retried after a failure: a module that failed to load once (offline before it
    // was ever cached) must not leave GPS broken for the rest of the session.
    private Task<IJSObjectReference>? _moduleTask;

    private Task<IJSObjectReference> Module()
    {
        if (_moduleTask is null || _moduleTask.IsFaulted || _moduleTask.IsCanceled)
            _moduleTask = js.InvokeAsync<IJSObjectReference>("import", "./_content/PharmaERP.FieldApp.UI/js/geo.js").AsTask();
        return _moduleTask;
    }

    private DotNetObjectReference<BrowserLocationService>? _ref;
    private IProgress<double>? _progress;
    private readonly SemaphoreSlim _fixLock = new(1, 1);

    public event Action<GeoFix>? FixReceived;
    public event Action<GeoError, string>? WatchError;

    private DotNetObjectReference<BrowserLocationService> Ref => _ref ??= DotNetObjectReference.Create(this);

    public async Task<GeoPermission> GetPermissionAsync() =>
        await (await Module()).InvokeAsync<string>("queryPermission") switch
        {
            "granted" => GeoPermission.Granted,
            "denied" => GeoPermission.Denied,
            "prompt" => GeoPermission.Prompt,
            "unsupported" => GeoPermission.Unsupported,
            _ => GeoPermission.Unknown
        };

    public async Task<GeoFixResult> GetBestFixAsync(double targetAccuracyMeters, int maxWaitMs,
        IProgress<double>? accuracyProgress = null, CancellationToken ct = default)
    {
        // One event fix at a time: the progress callback is shared, and two simultaneous high-accuracy
        // watches would only compete for the same GPS.
        await _fixLock.WaitAsync(ct);
        try
        {
            _progress = accuracyProgress;
            var module = await Module();
            // JS enforces maxWaitMs itself; the extra margin only guards against a stuck interop call.
            var result = await module.InvokeAsync<JsFixResult>("getBestFix",
                TimeSpan.FromMilliseconds(maxWaitMs + 5000), [Ref, targetAccuracyMeters, maxWaitMs]);
            return result is { Ok: true, Fix: { } fix }
                ? GeoFixResult.Success(fix)
                : GeoFixResult.Failure(MapError(result.Code), result.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return GeoFixResult.Failure(GeoError.Timeout, "Location request timed out.");
        }
        catch (JSException ex)
        {
            // e.g. geo.js couldn't be loaded (offline before it was ever cached). A missing fix is an ordinary
            // outcome for every caller — it must never surface as an unhandled exception that stops the app.
            return GeoFixResult.Failure(GeoError.Unavailable, ex.Message);
        }
        finally
        {
            _progress = null;
            _fixLock.Release();
        }
    }

    public async Task AcceptCurrentFixAsync() => await (await Module()).InvokeVoidAsync("acceptCurrentFix");

    public async Task<GeoError> StartWatchAsync(GeoFixSource source)
    {
        var result = await (await Module()).InvokeAsync<JsFixResult>("startWatch", Ref, source.ToString());
        return result.Ok ? GeoError.None : MapError(result.Code);
    }

    public async Task StopWatchAsync() => await (await Module()).InvokeVoidAsync("stopWatch");

    public async Task<bool> SetWakeLockAsync(bool enabled) =>
        await (await Module()).InvokeAsync<bool>("setWakeLock", enabled);

    public async Task<double?> GetBatteryLevelAsync() =>
        await (await Module()).InvokeAsync<double?>("batteryLevel");

    public async Task<string> GetPlatformAsync() => await (await Module()).InvokeAsync<string>("platform");

    [JSInvokable]
    public void OnFixProgress(double bestAccuracy, int samples) => _progress?.Report(bestAccuracy);

    [JSInvokable]
    public void OnTrackFix(GeoFix fix) => FixReceived?.Invoke(fix);

    [JSInvokable]
    public void OnTrackError(int code, string message) => WatchError?.Invoke(MapError(code), message);

    private static GeoError MapError(int code) => code switch
    {
        0 => GeoError.Unsupported,
        1 => GeoError.Denied,
        2 => GeoError.Unavailable,
        3 => GeoError.Timeout,
        4 => GeoError.Insecure,
        _ => GeoError.Unavailable
    };

    private sealed record JsFixResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("fix")] GeoFix? Fix,
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message);

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask is { IsCompletedSuccessfully: true })
        {
            var module = _moduleTask.Result;
            await module.InvokeVoidAsync("stopWatch");
            await module.DisposeAsync();
        }
        _ref?.Dispose();
    }
}

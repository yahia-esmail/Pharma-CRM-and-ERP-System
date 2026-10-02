using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaERP.Application.Location;
using PharmaERP.FieldApp.UI.Services.Offline;

namespace PharmaERP.FieldApp.UI.Services.Location;

public enum TrackingState
{
    /// <summary>Not started (signed out).</summary>
    Off,
    /// <summary>The rep hasn't agreed to working-hours tracking yet.</summary>
    NeedsConsent,
    OutsideWorkingHours,
    /// <summary>The app is in the background — browsers stop geolocation there anyway (plan 2.2).</summary>
    PausedInBackground,
    PermissionDenied,
    /// <summary>Location services off on the device, insecure origin, or unsupported browser.</summary>
    Unavailable,
    Tracking
}

/// <summary>Where the tracker keeps its consent flag and not-yet-uploaded points (IndexedDB in the
/// app, memory in tests). Points are persisted as they arrive, so closing the app loses nothing.</summary>
public interface ITrackerStorage
{
    Task<bool> GetConsentAsync();
    Task SetConsentAsync(bool granted);
    Task<List<LocationPingRequest>> GetBufferAsync();
    Task SetBufferAsync(List<LocationPingRequest> points);
}

/// <summary>Foreground tracking (plan 7.4) and Route Mode (7.5). While the app is visible, inside working
/// hours and with the rep's consent, fixes from <see cref="ILocationService"/> pass through
/// <see cref="LocationFilter"/>, are buffered locally, and are handed to the <see cref="Outbox"/> as one
/// <c>POST Location/pings</c> batch every <see cref="GpsOptions.UploadIntervalSeconds"/> or
/// <see cref="GpsOptions.UploadBatchSize"/> points — which makes the upload offline-safe and idempotent
/// for free. Tracking stops when the app is hidden; nothing here pretends to work in the background.</summary>
public sealed class LocationTracker : IAsyncDisposable
{
    public const string OutboxKind = "LocationBatch";

    private readonly ILocationService _location;
    private readonly ITrackerStorage _storage;
    private readonly Outbox _outbox;
    private readonly GpsOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<LocationTracker> _logger;
    private readonly LocationFilter _filter;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private bool _started;
    private bool _visible = true;
    private bool _consent;
    private bool _watching;
    private GeoError _lastWatchError;
    private DateTime _lastFlushUtc;

    public LocationTracker(ILocationService location, ITrackerStorage storage, Outbox outbox,
        IOptions<GpsOptions> options, TimeProvider time, ILogger<LocationTracker> logger)
    {
        _location = location;
        _storage = storage;
        _outbox = outbox;
        _options = options.Value;
        _time = time;
        _logger = logger;
        _filter = new LocationFilter(_options);
    }

    public TrackingState State { get; private set; } = TrackingState.Off;
    public bool RouteMode { get; private set; }
    public bool WakeLockActive { get; private set; }
    public GeoFix? LastFix { get; private set; }
    public FilterDecision? LastDecision { get; private set; }
    public int BufferedCount { get; private set; }
    public int KeptToday { get; private set; }
    public double RouteDistanceMeters { get; private set; }

    /// <summary>Raised on state changes and on every fix — drives the GPS badge and diagnostics.</summary>
    public event Action? Changed;

    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_started) return;
            _started = true;
            _location.FixReceived += OnFix;
            _location.WatchError += OnWatchError;
            _consent = await _storage.GetConsentAsync();
            BufferedCount = (await _storage.GetBufferAsync()).Count;
            _lastFlushUtc = Now;
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_started) return;
            _started = false;
            _location.FixReceived -= OnFix;
            _location.WatchError -= OnWatchError;
            await StopWatchAsync();
            await SetWakeLockAsync(false);
            RouteMode = false;
            await FlushCoreAsync();
            SetState(TrackingState.Off);
        }
        finally { _lock.Release(); }
    }

    /// <summary>The rep agreed (or withdrew) on the disclosure screen.</summary>
    public async Task SetConsentAsync(bool granted)
    {
        await _lock.WaitAsync();
        try
        {
            _consent = granted;
            await _storage.SetConsentAsync(granted);
            _lastWatchError = GeoError.None;
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>An event fix (not the watch) found permission blocked — reflect it right away rather than
    /// waiting for the watch to report the same.</summary>
    public async Task ReportPermissionDeniedAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _lastWatchError = GeoError.Denied;
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Route Mode: keeps the screen on and tags points as "Route", for reps who drive with the
    /// app open. It is an explicit choice, so it also tracks outside the configured working hours.</summary>
    public async Task SetRouteModeAsync(bool enabled)
    {
        await _lock.WaitAsync();
        try
        {
            if (RouteMode == enabled) return;
            RouteMode = enabled;
            if (enabled) RouteDistanceMeters = 0;
            await StopWatchAsync();   // restart with the new source tag
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task OnVisibilityChangedAsync(bool visible)
    {
        await _lock.WaitAsync();
        try
        {
            _visible = visible;
            if (!visible)
            {
                // Hand the buffer to the outbox before the browser freezes us: the outbox is what
                // syncs first when the app next opens.
                await FlushCoreAsync();
            }
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Called periodically (every ~30 s) by the app: starts/stops at working-hours boundaries
    /// and uploads the buffer once the interval has passed.</summary>
    public async Task TickAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await ReconcileAsync();
            if (Now - _lastFlushUtc >= TimeSpan.FromSeconds(_options.UploadIntervalSeconds))
                await FlushCoreAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Stores an event fix (check-in, order…) as a route point too, so the route between visits is
    /// complete even when tracking is off.</summary>
    public async Task RecordEventFixAsync(GeoFix fix)
    {
        await _lock.WaitAsync();
        try
        {
            LastFix = fix;
            await AppendAsync(fix with { Source = GeoFixSource.Event }, isAnomaly: false);
        }
        finally { _lock.Release(); }
        Changed?.Invoke();
    }

    public async Task FlushAsync()
    {
        await _lock.WaitAsync();
        try { await FlushCoreAsync(); }
        finally { _lock.Release(); }
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private bool WithinWorkingHours()
    {
        var local = TimeOnly.FromDateTime(_time.GetLocalNow().DateTime);
        return _options.WorkingHoursStart <= _options.WorkingHoursEnd
            ? local >= _options.WorkingHoursStart && local < _options.WorkingHoursEnd
            : local >= _options.WorkingHoursStart || local < _options.WorkingHoursEnd;   // overnight shift
    }

    private TrackingState DesiredState()
    {
        if (!_started) return TrackingState.Off;
        if (!_consent) return TrackingState.NeedsConsent;
        if (_lastWatchError == GeoError.Denied) return TrackingState.PermissionDenied;
        if (_lastWatchError is GeoError.Unsupported or GeoError.Insecure) return TrackingState.Unavailable;
        if (!RouteMode && !WithinWorkingHours()) return TrackingState.OutsideWorkingHours;
        if (!_visible) return TrackingState.PausedInBackground;
        return TrackingState.Tracking;
    }

    private async Task ReconcileAsync()
    {
        var desired = DesiredState();
        if (desired == TrackingState.Tracking)
        {
            if (!_watching)
            {
                var error = await _location.StartWatchAsync(RouteMode ? GeoFixSource.Route : GeoFixSource.Track);
                if (error == GeoError.None)
                {
                    _watching = true;
                    _filter.Reset();   // the gap while stopped is not movement
                }
                else
                {
                    _lastWatchError = error;
                    desired = DesiredState();
                }
            }
            if (_lastWatchError == GeoError.Unavailable) desired = TrackingState.Unavailable;
        }
        else
        {
            await StopWatchAsync();
        }

        await SetWakeLockAsync(RouteMode && _visible && desired == TrackingState.Tracking);
        SetState(desired);
    }

    private async Task StopWatchAsync()
    {
        if (!_watching) return;
        _watching = false;
        await _location.StopWatchAsync();
    }

    private async Task SetWakeLockAsync(bool enabled)
    {
        if (WakeLockActive == enabled) return;
        WakeLockActive = enabled && await _location.SetWakeLockAsync(true);
        if (!enabled) await _location.SetWakeLockAsync(false);
    }

    private void SetState(TrackingState state)
    {
        if (State == state) return;
        _logger.LogInformation("GPS tracking: {From} → {To}", State, state);
        State = state;
        Changed?.Invoke();
    }

    private void OnFix(GeoFix fix) => _ = HandleFixAsync(fix);

    private async Task HandleFixAsync(GeoFix fix)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_watching) return;   // late callback after stop
            var previous = _filter.LastKept;
            var decision = _filter.Evaluate(fix);
            LastFix = fix;
            LastDecision = decision;

            if (_lastWatchError != GeoError.None)
            {
                _lastWatchError = GeoError.None;   // a fix proves location works again
                await ReconcileAsync();
            }

            if (decision is FilterDecision.Keep or FilterDecision.Anomaly)
            {
                if (decision == FilterDecision.Keep && previous is not null && RouteMode)
                    RouteDistanceMeters += GeoMath.DistanceMeters(previous, fix);
                await AppendAsync(fix, isAnomaly: decision == FilterDecision.Anomaly);
                if (BufferedCount >= _options.UploadBatchSize) await FlushCoreAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle GPS fix");
        }
        finally { _lock.Release(); }
        Changed?.Invoke();
    }

    private void OnWatchError(GeoError error, string message) => _ = HandleWatchErrorAsync(error, message);

    private async Task HandleWatchErrorAsync(GeoError error, string message)
    {
        // A per-reading timeout while tracking is routine (tunnel, building); only persistent problems
        // change the state.
        if (error == GeoError.Timeout) return;
        await _lock.WaitAsync();
        try
        {
            // "Unavailable" right after a good reading is a momentary signal loss, not location switched off.
            if (error == GeoError.Unavailable && LastFix is { } last
                && Now - last.DeviceTimestampUtc < TimeSpan.FromSeconds(30))
                return;
            _logger.LogWarning("GPS watch error {Error}: {Message}", error, message);
            _lastWatchError = error;
            if (error == GeoError.Denied) await StopWatchAsync();
            await ReconcileAsync();
        }
        finally { _lock.Release(); }
    }

    private async Task AppendAsync(GeoFix fix, bool isAnomaly)
    {
        var buffer = await _storage.GetBufferAsync();
        buffer.Add(new LocationPingRequest
        {
            ClientId = Guid.NewGuid(),
            Latitude = fix.Latitude,
            Longitude = fix.Longitude,
            TimestampUtc = fix.DeviceTimestampUtc,
            AccuracyMeters = Math.Round(fix.AccuracyMeters, 1),
            AltitudeMeters = fix.AltitudeMeters,
            SpeedMps = fix.SpeedMps,
            Heading = fix.Heading,
            Source = fix.Source.ToString(),
            IsAnomaly = isAnomaly
        });
        await _storage.SetBufferAsync(buffer);
        BufferedCount = buffer.Count;
        KeptToday++;
    }

    private async Task FlushCoreAsync()
    {
        _lastFlushUtc = Now;
        var buffer = await _storage.GetBufferAsync();
        if (buffer.Count == 0) return;

        foreach (var chunk in buffer.Chunk(LocationPingBatchRequest.MaxPoints))
        {
            await _outbox.EnqueueAsync(new OutboxRequest(OutboxKind, $"Route points ({chunk.Length})", "POST",
                "api/v1/Location/pings", new LocationPingBatchRequest { Points = [.. chunk] }));
        }
        // Only cleared once the outbox holds the points — a crash in between re-sends them, and the
        // server skips the duplicates by clientId.
        await _storage.SetBufferAsync([]);
        BufferedCount = 0;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

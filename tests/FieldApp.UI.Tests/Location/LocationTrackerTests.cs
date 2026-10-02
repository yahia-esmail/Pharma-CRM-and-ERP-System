using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Location;
using PharmaERP.FieldApp.UI.Services;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Tests.Offline;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;

namespace PharmaERP.FieldApp.UI.Tests.Location;

public class LocationTrackerTests
{
    private readonly FakeLocationService _gps = new();
    private readonly InMemoryTrackerStorage _storage = new();
    private readonly InMemoryOutboxStore _outboxStore = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(T0));
    private readonly GpsOptions _options = new()
    {
        UploadBatchSize = 3,
        UploadIntervalSeconds = 120,
        TrackDistanceMeters = 50,
        TrackIntervalSeconds = 180,
        WorkingHoursStart = new TimeOnly(8, 0),
        WorkingHoursEnd = new TimeOnly(20, 0)
    };
    private readonly LocationTracker _tracker;

    public LocationTrackerTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);   // 09:00 local — inside working hours
        _tracker = new LocationTracker(_gps, _storage, new Outbox(_outboxStore, _clock), Options.Create(_options),
            _clock, NullLogger<LocationTracker>.Instance);
    }

    private async Task StartWithConsentAsync()
    {
        _storage.Consent = true;
        await _tracker.StartAsync();
    }

    private IReadOnlyList<LocationPingRequest> UploadedPoints() =>
        _outboxStore.Items.Where(i => i.Kind == LocationTracker.OutboxKind)
            .SelectMany(i => JsonSerializer.Deserialize<LocationPingBatchRequest>(i.JsonBody!, ApiJson.Options)!.Points)
            .ToList();

    [Fact]
    public async Task Does_not_track_without_consent()
    {
        await _tracker.StartAsync();

        Assert.Equal(TrackingState.NeedsConsent, _tracker.State);
        Assert.False(_gps.Watching);
    }

    [Fact]
    public async Task Consent_during_working_hours_starts_tracking()
    {
        await _tracker.StartAsync();
        await _tracker.SetConsentAsync(true);

        Assert.Equal(TrackingState.Tracking, _tracker.State);
        Assert.True(_gps.Watching);
        Assert.Equal(GeoFixSource.Track, _gps.WatchSource);
        Assert.True(_storage.Consent);
    }

    [Fact]
    public async Task Outside_working_hours_nothing_is_tracked_unless_route_mode_is_on()
    {
        _clock.SetUtcNow(new DateTimeOffset(2026, 9, 29, 21, 0, 0, TimeSpan.Zero));
        await StartWithConsentAsync();
        Assert.Equal(TrackingState.OutsideWorkingHours, _tracker.State);
        Assert.False(_gps.Watching);

        await _tracker.SetRouteModeAsync(true);

        Assert.Equal(TrackingState.Tracking, _tracker.State);
        Assert.Equal(GeoFixSource.Route, _gps.WatchSource);
        Assert.True(_gps.WakeLockOn);
    }

    [Fact]
    public async Task Tracking_stops_at_the_end_of_working_hours()
    {
        _clock.SetUtcNow(new DateTimeOffset(2026, 9, 29, 19, 59, 0, TimeSpan.Zero));
        await StartWithConsentAsync();
        Assert.Equal(TrackingState.Tracking, _tracker.State);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await _tracker.TickAsync();

        Assert.Equal(TrackingState.OutsideWorkingHours, _tracker.State);
        Assert.False(_gps.Watching);
    }

    [Fact]
    public async Task Going_to_the_background_pauses_and_hands_buffered_points_to_the_outbox()
    {
        await StartWithConsentAsync();
        _gps.Emit(Fix());

        await _tracker.OnVisibilityChangedAsync(false);

        Assert.Equal(TrackingState.PausedInBackground, _tracker.State);
        Assert.False(_gps.Watching);
        Assert.Single(UploadedPoints());
        Assert.Empty(_storage.Buffer);

        await _tracker.OnVisibilityChangedAsync(true);
        Assert.Equal(TrackingState.Tracking, _tracker.State);
        Assert.True(_gps.Watching);
    }

    [Fact]
    public async Task Kept_points_are_persisted_immediately_and_uploaded_as_one_batch_when_full()
    {
        await StartWithConsentAsync();

        _gps.Emit(Fix(seconds: 0));
        _gps.Emit(Fix(north: 10, seconds: 10));    // skipped: too close, too soon
        _gps.Emit(Fix(north: 100, seconds: 20));
        Assert.Equal(2, _storage.Buffer.Count);    // survives an app kill right now
        Assert.Empty(_outboxStore.Items);

        _gps.Emit(Fix(north: 200, seconds: 30));   // third kept point = batch size

        var batch = Assert.Single(_outboxStore.Items);
        Assert.Equal("api/v1/Location/pings", batch.Url);
        var points = UploadedPoints();
        Assert.Equal(3, points.Count);
        Assert.Equal(3, points.Select(p => p.ClientId).Distinct().Count());
        Assert.All(points, p => Assert.Equal("Track", p.Source));
        Assert.Empty(_storage.Buffer);
    }

    [Fact]
    public async Task Partial_buffer_is_uploaded_once_the_interval_passes()
    {
        await StartWithConsentAsync();
        _gps.Emit(Fix());

        _clock.Advance(TimeSpan.FromSeconds(60));
        await _tracker.TickAsync();
        Assert.Empty(_outboxStore.Items);

        _clock.Advance(TimeSpan.FromSeconds(61));
        await _tracker.TickAsync();
        Assert.Single(UploadedPoints());
    }

    [Fact]
    public async Task Impossible_jumps_are_uploaded_flagged_as_anomalies()
    {
        await StartWithConsentAsync();
        _gps.Emit(Fix());
        _gps.Emit(Fix(north: 8000, seconds: 20));   // 1440 km/h

        await _tracker.FlushAsync();

        var points = UploadedPoints();
        Assert.Equal([false, true], points.Select(p => p.IsAnomaly));
    }

    [Fact]
    public async Task Permission_denied_while_tracking_stops_the_watch()
    {
        await StartWithConsentAsync();

        _gps.EmitError(GeoError.Denied);

        Assert.Equal(TrackingState.PermissionDenied, _tracker.State);
        Assert.False(_gps.Watching);
    }

    [Fact]
    public async Task A_lost_signal_timeout_does_not_change_the_state()
    {
        await StartWithConsentAsync();

        _gps.EmitError(GeoError.Timeout);

        Assert.Equal(TrackingState.Tracking, _tracker.State);
        Assert.True(_gps.Watching);
    }

    [Fact]
    public async Task Event_fixes_are_recorded_as_route_points_even_when_not_tracking()
    {
        await _tracker.StartAsync();   // no consent → not tracking

        await _tracker.RecordEventFixAsync(Fix(accuracy: 8));
        await _tracker.FlushAsync();

        var point = Assert.Single(UploadedPoints());
        Assert.Equal("Event", point.Source);
        Assert.Equal(8, point.AccuracyMeters);
    }

    [Fact]
    public async Task Stopping_flushes_and_turns_everything_off()
    {
        await StartWithConsentAsync();
        await _tracker.SetRouteModeAsync(true);
        _gps.Emit(Fix());

        await _tracker.StopAsync();

        Assert.Equal(TrackingState.Off, _tracker.State);
        Assert.False(_gps.Watching);
        Assert.False(_gps.WakeLockOn);
        Assert.Single(UploadedPoints());
    }
}

internal sealed class FakeLocationService : ILocationService
{
    public bool Watching { get; private set; }
    public GeoFixSource? WatchSource { get; private set; }
    public bool WakeLockOn { get; private set; }
    public GeoError StartError { get; set; }

    public event Action<GeoFix>? FixReceived;
    public event Action<GeoError, string>? WatchError;

    public void Emit(GeoFix fix)
    {
        if (Watching) FixReceived?.Invoke(fix);
    }

    public void EmitError(GeoError error) => WatchError?.Invoke(error, error.ToString());

    public Task<GeoPermission> GetPermissionAsync() => Task.FromResult(GeoPermission.Granted);

    public Task<GeoFixResult> GetBestFixAsync(double targetAccuracyMeters, int maxWaitMs,
        IProgress<double>? accuracyProgress = null, CancellationToken ct = default) =>
        Task.FromResult(GeoFixResult.Success(Fix(accuracy: targetAccuracyMeters, source: GeoFixSource.Event)));

    public Task AcceptCurrentFixAsync() => Task.CompletedTask;

    public Task<GeoError> StartWatchAsync(GeoFixSource source)
    {
        if (StartError != GeoError.None) return Task.FromResult(StartError);
        Watching = true;
        WatchSource = source;
        return Task.FromResult(GeoError.None);
    }

    public Task StopWatchAsync()
    {
        Watching = false;
        return Task.CompletedTask;
    }

    public Task<bool> SetWakeLockAsync(bool enabled)
    {
        WakeLockOn = enabled;
        return Task.FromResult(enabled);
    }

    public Task<double?> GetBatteryLevelAsync() => Task.FromResult<double?>(null);
    public Task<string> GetPlatformAsync() => Task.FromResult("android");
}

internal sealed class InMemoryTrackerStorage : ITrackerStorage
{
    public bool Consent { get; set; }
    public List<LocationPingRequest> Buffer { get; private set; } = [];

    public Task<bool> GetConsentAsync() => Task.FromResult(Consent);
    public Task SetConsentAsync(bool granted) { Consent = granted; return Task.CompletedTask; }
    public Task<List<LocationPingRequest>> GetBufferAsync() => Task.FromResult(Buffer.ToList());
    public Task SetBufferAsync(List<LocationPingRequest> points) { Buffer = points.ToList(); return Task.CompletedTask; }
}

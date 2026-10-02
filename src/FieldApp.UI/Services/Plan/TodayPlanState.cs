using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Plan;

/// <summary>Today's Plan as the rep sees it (plan phase 5): the cached plan from <see cref="MasterDataSync"/>,
/// visits recorded on this phone but not yet synced (so a stop turns "Done" the moment it is visited, even
/// offline), and distances from the rep's latest GPS position. Shared by the Plan screen and the dashboard.</summary>
public sealed class TodayPlanState : IDisposable
{
    // Recomputing distances on every GPS callback would re-render the list constantly; a stop's distance
    // only needs refreshing once the rep has actually moved.
    private const double RecomputeAfterMeters = 25;

    private readonly MasterDataSync _masterData;
    private readonly KeyValueStore _storage;
    private readonly LocationTracker _tracker;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Cached<IReadOnlyList<PharmaERP.Application.VisitPlans.VisitPlanItemDto>>? _plan;
    private Dictionary<int, DateTime> _localVisits = [];
    private GeoFix? _computedFor;
    private bool _loaded;

    public TodayPlanState(MasterDataSync masterData, KeyValueStore storage, LocationTracker tracker, TimeProvider time)
    {
        _masterData = masterData;
        _storage = storage;
        _tracker = tracker;
        _time = time;
        _masterData.Synced += OnMasterDataSynced;
        _tracker.Changed += OnTrackerChanged;
    }

    public IReadOnlyList<PlanStop> Stops { get; private set; } = [];
    public RouteSummary Route { get; private set; } = new(0, 0, 0, TimeSpan.Zero);
    public DateTime? SavedAtUtc => _plan?.SavedAtUtc;
    public bool HasData => _plan is not null;
    public GeoFix? Position => _tracker.LastFix;

    public int DoneCount => Stops.Count(s => s.Status == StopStatus.Done);
    public PlanStop? NextStop => Stops.FirstOrDefault(s => s.Status == StopStatus.Next);

    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    /// <summary>Pull the plan from the server now (falls back to the cached copy when offline).</summary>
    public async Task<bool> RefreshAsync()
    {
        await _masterData.RefreshAsync(MasterDataSync.TodayPlan);
        await ReloadAsync();
        return _masterData.LastError(MasterDataSync.TodayPlan) is null;
    }

    /// <summary>Called by the visit screens (phase 6) right after a visit for this stop is queued, so the
    /// plan reflects it before the outbox has delivered it.</summary>
    public async Task MarkVisitedLocallyAsync(int planItemId)
    {
        // Not loaded yet means _localVisits is empty — saving it would wipe earlier marks from today.
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            _localVisits[planItemId] = _time.GetUtcNow().UtcDateTime;
            await _storage.SetAsync(LocalVisitsKey, _localVisits);
            Recompute();
        }
        finally { _lock.Release(); }
        Changed?.Invoke();
    }

    // Keyed by day so yesterday's local marks never leak into today's plan.
    /// <summary>Forget everything in memory (sign-out; storage is wiped separately).</summary>
    public void Reset()
    {
        _plan = null;
        _localVisits = [];
        _computedFor = null;
        _loaded = false;
        Stops = [];
        Route = new(0, 0, 0, TimeSpan.Zero);
        Changed?.Invoke();
    }

    // The phone's local date, matching the server's business day (not UTC, which lags Cairo by 2–3 hours).
    private string LocalVisitsKey => $"plan:localVisits:{DateOnly.FromDateTime(_time.GetLocalNow().DateTime):yyyy-MM-dd}";

    private async Task ReloadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _plan = await _masterData.GetTodayPlanAsync();
            _localVisits = await _storage.GetAsync<Dictionary<int, DateTime>>(LocalVisitsKey) ?? [];
            _loaded = true;
            Recompute();
        }
        finally { _lock.Release(); }
        Changed?.Invoke();
    }

    private void Recompute()
    {
        _computedFor = _tracker.LastFix;
        Stops = PlanLogic.BuildStops(_plan?.Data ?? [], _localVisits, _computedFor);
        Route = PlanLogic.SummarizeRoute(Stops, _computedFor);
    }

    private void OnMasterDataSynced() => _ = ReloadAsync();

    private void OnTrackerChanged()
    {
        if (!_loaded || _tracker.LastFix is not { } fix) return;
        if (_computedFor is not null && GeoMath.DistanceMeters(_computedFor, fix) < RecomputeAfterMeters) return;
        Recompute();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _masterData.Synced -= OnMasterDataSynced;
        _tracker.Changed -= OnTrackerChanged;
    }
}

using PharmaERP.Application.Visits;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Visits;

/// <summary>Runs the visit flow on the phone (plan phase 6): check-in → timer/draft → check-out, one visit at
/// a time. Every step is an outbox item, so a visit works identically with or without signal:
///
///   check-in  POST {Doctors|Pharmacies}/{id}/visits/check-in                 produces ref visit:{localId}
///   check-out POST {Doctors|Pharmacies}/{id}/visits/{ref:visit:…}/check-out   depends on the check-in
///
/// The server times the visit from the two device timestamps (corrected for clock offset), so the phone
/// never reports a duration it could fake.</summary>
public sealed class VisitSessionManager(
    KeyValueStore storage,
    Outbox outbox,
    TodayPlanState plan,
    MasterDataSync masterData,
    LocationTracker tracker,
    TimeProvider time)
{
    private const string OpenVisitKey = "visit:open";

    /// <summary>A customer's first location is applied by the server without review when at least this
    /// accurate (VisitValidationOptions.AutoApplyLocationMaxAccuracyMeters) — mirrored so the phone can use
    /// the new location right away.</summary>
    public const double AutoApplyLocationMaxAccuracyMeters = 30;

    private OpenVisit? _open;
    private bool _loaded;

    public event Action? Changed;

    public async Task<OpenVisit?> GetOpenAsync()
    {
        if (!_loaded)
        {
            _open = await storage.GetAsync<OpenVisit>(OpenVisitKey);
            _loaded = true;
        }
        return _open;
    }

    public OpenVisit? Current => _open;

    public async Task<OpenVisit> CheckInAsync(StopKind kind, int customerId, string customerName, int? planItemId,
        GeoFix? fix, GeofenceResult? geofence, string? outsideReason)
    {
        if (await GetOpenAsync() is { } existing)
            throw new InvalidOperationException($"A visit at {existing.CustomerName} is still open — complete or cancel it first.");

        var localId = Guid.NewGuid();
        var now = time.GetUtcNow().UtcDateTime;
        var controller = kind == StopKind.Doctor ? "Doctors" : "Pharmacies";

        var item = await outbox.EnqueueAsync(new OutboxRequest(
            Kind: kind == StopKind.Doctor ? "DoctorCheckIn" : "PharmacyCheckIn",
            Title: $"Check-in · {customerName}",
            Method: "POST",
            Url: $"api/v1/{controller}/{customerId}/visits/check-in",
            Body: new VisitCheckInRequest
            {
                VisitPlanItemId = planItemId,
                DeviceTimeUtc = now,
                Location = ToVisitFix(fix),
                OutsideGeofenceReason = outsideReason
            },
            ProducesRef: $"visit:{localId}"));

        var open = new OpenVisit
        {
            LocalId = localId,
            Kind = kind,
            CustomerId = customerId,
            CustomerName = customerName,
            PlanItemId = planItemId,
            CheckInDeviceUtc = now,
            CheckInFix = fix,
            CheckInGeofence = geofence?.Status,
            CheckInDistanceMeters = geofence?.DistanceMeters,
            OutsideReason = outsideReason,
            CheckInOutboxId = item.Id
        };
        await PersistAsync(open);
        return open;
    }

    /// <summary>Keeps what the rep typed so far (also called on the way out of the screen).</summary>
    public async Task SaveDraftAsync(OpenVisit open)
    {
        open.DraftSavedAtUtc = time.GetUtcNow().UtcDateTime;
        await PersistAsync(open);
    }

    public async Task CompleteAsync(OpenVisit open, GeoFix? checkOutFix, IReadOnlyDictionary<int, string> productNames)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var url = $"api/v1/{open.Controller}/{open.CustomerId}/visits/{OutboxRefs.Placeholder(open.VisitRef)}/check-out";
        object body = open.Kind == StopKind.Doctor
            ? new DoctorVisitCheckOutRequest
            {
                DeviceTimeUtc = now,
                Location = ToVisitFix(checkOutFix),
                // Free-text columns today (plan 6.2 item 10 proposes structured lines + custody deduction).
                ProductsDiscussed = Join(open.ProductsDiscussed.Select(id => productNames.GetValueOrDefault(id, $"#{id}"))),
                SamplesGiven = Join(open.Samples.Select(s => $"{s.ProductName} x{s.Quantity}")),
                FeedbackNotes = Blank(open.Notes),
                NextVisitRecommendation = Blank(open.NextVisitRecommendation),
                InterestLevel = open.InterestLevel
            }
            : new PharmacyVisitCheckOutRequest
            {
                DeviceTimeUtc = now,
                Location = ToVisitFix(checkOutFix),
                Purpose = open.Purpose,
                Notes = Blank(open.Notes)
            };

        await outbox.EnqueueAsync(new OutboxRequest(
            Kind: open.Kind == StopKind.Doctor ? "DoctorCheckOut" : "PharmacyCheckOut",
            Title: $"Visit · {open.CustomerName}",
            Method: "POST",
            Url: url,
            Body: body,
            DependsOn: open.CheckInOutboxId));

        if (checkOutFix is not null) await tracker.RecordEventFixAsync(checkOutFix);
        if (open.PlanItemId is { } planItemId) await plan.MarkVisitedLocallyAsync(planItemId);
        await ClearAsync();
    }

    /// <summary>Abandons the visit. If the check-in never left the phone it is simply dropped; otherwise the
    /// server-side visit is cancelled (soft-deleted) so it doesn't linger as "in progress".</summary>
    public async Task CancelAsync(OpenVisit open)
    {
        var checkInStillQueued = (await outbox.GetItemsAsync()).Any(i => i.Id == open.CheckInOutboxId);
        if (checkInStillQueued)
        {
            await outbox.DiscardAsync(open.CheckInOutboxId);
        }
        else
        {
            await outbox.EnqueueAsync(new OutboxRequest(
                Kind: "VisitCancel",
                Title: $"Cancel visit · {open.CustomerName}",
                Method: "POST",
                Url: $"api/v1/{open.Controller}/{open.CustomerId}/visits/{OutboxRefs.Placeholder(open.VisitRef)}/cancel"));
        }
        await ClearAsync();
    }

    /// <summary>Sends the rep's on-site fix as this customer's location (plan 7.9). A first location with a good
    /// fix is applied by the server at once, so it's also applied to the offline copy right away.</summary>
    public async Task<bool> ProposeLocationAsync(StopKind kind, int customerId, string customerName, GeoFix fix, bool customerHasLocation)
    {
        var controller = kind == StopKind.Doctor ? "Doctors" : "Pharmacies";
        await outbox.EnqueueAsync(new OutboxRequest(
            Kind: "LocationProposal",
            Title: $"Location · {customerName}",
            Method: "POST",
            Url: $"api/v1/{controller}/{customerId}/location-proposals",
            Body: new LocationProposalRequest
            {
                Latitude = fix.Latitude,
                Longitude = fix.Longitude,
                AccuracyMeters = Math.Round(fix.AccuracyMeters, 1),
                CapturedAtUtc = fix.DeviceTimestampUtc
            }));

        var appliesNow = !customerHasLocation && fix.AccuracyMeters <= AutoApplyLocationMaxAccuracyMeters;
        if (appliesNow) await masterData.SetCustomerLocationAsync(kind, customerId, fix.Latitude, fix.Longitude);
        return appliesNow;
    }

    /// <summary>Forget the in-memory copy (sign-out; storage is wiped separately).</summary>
    public void Reset()
    {
        _open = null;
        _loaded = false;
        Changed?.Invoke();
    }

    private async Task PersistAsync(OpenVisit open)
    {
        _open = open;
        _loaded = true;
        await storage.SetAsync(OpenVisitKey, open);
        Changed?.Invoke();
    }

    private async Task ClearAsync()
    {
        _open = null;
        _loaded = true;
        await storage.RemoveAsync(OpenVisitKey);
        Changed?.Invoke();
    }

    private static VisitFix? ToVisitFix(GeoFix? fix) => fix is null ? null : new VisitFix
    {
        Latitude = fix.Latitude,
        Longitude = fix.Longitude,
        AccuracyMeters = Math.Round(fix.AccuracyMeters, 1),
        DeviceTimestampUtc = fix.DeviceTimestampUtc,
        ElapsedMs = fix.ElapsedMs
    };

    private static string? Join(IEnumerable<string> parts) => string.Join(", ", parts) is { Length: > 0 } s ? s : null;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

using PharmaERP.Application.Doctors;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.VisitPlans;
using PharmaERP.FieldApp.UI.Services.Location;

namespace PharmaERP.FieldApp.UI.Services.Plan;

public enum StopKind { Doctor, Pharmacy }

public enum StopStatus
{
    /// <summary>A visit linked to this stop exists (on the server, or recorded on this phone and not yet synced).</summary>
    Done,
    /// <summary>The first stop, in plan order, that isn't done.</summary>
    Next,
    Pending
}

/// <summary>One row of Today's Plan, with everything the list and map need.</summary>
public sealed record PlanStop(
    VisitPlanItemDto Item,
    StopKind Kind,
    StopStatus Status,
    DateTime? VisitedAtUtc,
    bool VisitedLocallyOnly,
    double? DistanceMeters)
{
    public int CustomerId => Kind == StopKind.Doctor ? Item.DoctorId!.Value : Item.PharmacyId!.Value;
    public string Name => (Kind == StopKind.Doctor ? Item.DoctorName : Item.PharmacyName) ?? "(unnamed)";
    public bool HasLocation => Item.Latitude is not null && Item.Longitude is not null;

    /// <summary>Route to the visit screen for this stop (phase 6), carrying the plan item for linking.</summary>
    public string VisitUrl => Kind == StopKind.Doctor
        ? $"visits/doctor/{CustomerId}?planItem={Item.Id}"
        : $"visits/pharmacy/{CustomerId}?planItem={Item.Id}";
}

/// <summary>A customer near the rep, for adding an unplanned (ad-hoc) visit.</summary>
public sealed record NearbyCustomer(StopKind Kind, int Id, string Name, string? Detail, double Latitude, double Longitude,
    double DistanceMeters, bool InTodaysPlan)
{
    public string VisitUrl => Kind == StopKind.Doctor ? $"visits/doctor/{Id}" : $"visits/pharmacy/{Id}";
}

/// <param name="RemainingStops">Stops not done yet.</param>
/// <param name="MappedStops">Remaining stops that have coordinates (only those count toward distance).</param>
/// <param name="DistanceMeters">Straight-line distance from the rep (if known) through the remaining mapped
/// stops in plan order.</param>
/// <param name="DriveTime">Estimate at an average city speed — a rough guide, not navigation.</param>
public sealed record RouteSummary(int RemainingStops, int MappedStops, double DistanceMeters, TimeSpan DriveTime);

/// <summary>Pure planning logic, kept free of UI and storage so it can be unit-tested.</summary>
public static class PlanLogic
{
    /// <summary>Average door-to-door speed in Cairo traffic, including parking — used only for the ETA hint.</summary>
    public const double AverageCitySpeedKmh = 18;

    public static IReadOnlyList<PlanStop> BuildStops(IEnumerable<VisitPlanItemDto> items,
        IReadOnlyDictionary<int, DateTime> localVisits, GeoFix? position)
    {
        var stops = new List<PlanStop>();
        var nextAssigned = false;
        foreach (var item in items.Where(i => i.DoctorId is not null || i.PharmacyId is not null).OrderBy(i => i.Sequence))
        {
            var local = localVisits.TryGetValue(item.Id, out var at) ? at : (DateTime?)null;
            var visitedAt = item.VisitedAtUtc ?? local;
            var status = visitedAt is not null ? StopStatus.Done
                : !nextAssigned ? StopStatus.Next
                : StopStatus.Pending;
            if (status == StopStatus.Next) nextAssigned = true;

            double? distance = position is not null && item.Latitude is { } lat && item.Longitude is { } lon
                ? GeoMath.DistanceMeters(position.Latitude, position.Longitude, lat, lon)
                : null;

            stops.Add(new PlanStop(item, item.DoctorId is not null ? StopKind.Doctor : StopKind.Pharmacy, status,
                visitedAt, item.VisitedAtUtc is null && local is not null, distance));
        }
        return stops;
    }

    public static RouteSummary SummarizeRoute(IReadOnlyList<PlanStop> stops, GeoFix? position)
    {
        var remaining = stops.Where(s => s.Status != StopStatus.Done).ToList();
        var mapped = remaining.Where(s => s.HasLocation).Select(s => (s.Item.Latitude!.Value, s.Item.Longitude!.Value)).ToList();

        var path = position is null ? mapped : [(position.Latitude, position.Longitude), .. mapped];
        var meters = GeoMath.PathLengthMeters(path);
        var drive = TimeSpan.FromHours(meters / 1000 / AverageCitySpeedKmh);
        return new RouteSummary(remaining.Count, mapped.Count, meters, TimeSpan.FromMinutes(Math.Round(drive.TotalMinutes)));
    }

    public static IReadOnlyList<NearbyCustomer> FindNearby(GeoFix position, IEnumerable<DoctorListItemDto> doctors,
        IEnumerable<PharmacyListItemDto> pharmacies, IEnumerable<PlanStop> todaysStops, int take = 15,
        double maxDistanceMeters = 5000)
    {
        var planned = todaysStops.Select(s => (s.Kind, s.CustomerId)).ToHashSet();

        var candidates = doctors
            .Where(d => d.Latitude is not null && d.Longitude is not null)
            .Select(d => (Kind: StopKind.Doctor, d.Id, Name: d.FullName,
                Detail: string.Join(" · ", new[] { d.Specialty, d.ClassificationName }.Where(x => !string.IsNullOrWhiteSpace(x))),
                Lat: d.Latitude!.Value, Lon: d.Longitude!.Value))
            .Concat(pharmacies
                .Where(p => p.Latitude is not null && p.Longitude is not null)
                .Select(p => (Kind: StopKind.Pharmacy, p.Id, p.Name, Detail: p.Segment ?? "Pharmacy",
                    Lat: p.Latitude!.Value, Lon: p.Longitude!.Value)));

        return candidates
            .Select(c => new NearbyCustomer(c.Kind, c.Id, c.Name, string.IsNullOrEmpty(c.Detail) ? null : c.Detail, c.Lat, c.Lon,
                GeoMath.DistanceMeters(position.Latitude, position.Longitude, c.Lat, c.Lon), planned.Contains((c.Kind, c.Id))))
            .Where(n => n.DistanceMeters <= maxDistanceMeters)
            .OrderBy(n => n.DistanceMeters)
            .Take(take)
            .ToList();
    }

    public static string FormatDuration(TimeSpan span) =>
        span.TotalMinutes < 60 ? $"{span.TotalMinutes:0} min" : $"{(int)span.TotalHours} h {span.Minutes:00} min";

    /// <summary>Google Maps directions link — opens the Maps app on Android/iOS when installed.</summary>
    public static string DirectionsUrl(double latitude, double longitude) =>
        FormattableString.Invariant($"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}");
}

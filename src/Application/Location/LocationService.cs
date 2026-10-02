using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Location;

public class LocationService(IAppDbContext db, IBusinessCalendar calendar) : ILocationService
{
    // A fix claiming to be further in the future than this is a wrong device clock, not clock drift.
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(10);

    // Points older than this aren't route data any more (refresh tokens last 14 days, so a phone that
    // stayed offline longer can't upload anyway).
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(15);

    public async Task RecordPingAsync(int representativeId, LocationPingRequest request, CancellationToken ct = default)
    {
        await RecordPingsAsync(representativeId, [request], ct);
    }

    public async Task<LocationPingBatchResult> RecordPingsAsync(int representativeId,
        IReadOnlyList<LocationPingRequest> points, CancellationToken ct = default)
    {
        if (points.Count == 0) return new LocationPingBatchResult(0, 0);
        if (points.Count > LocationPingBatchRequest.MaxPoints)
            throw new ValidationFailedException($"A batch can hold at most {LocationPingBatchRequest.MaxPoints} points.");

        var receivedAtUtc = DateTime.UtcNow;
        for (var i = 0; i < points.Count; i++)
            Validate(points[i], i, receivedAtUtc);

        // Idempotent per point: skip client ids already stored (a batch re-sent after a lost response)
        // and duplicates inside the batch itself.
        var clientIds = points.Where(p => p.ClientId is not null).Select(p => p.ClientId!.Value).Distinct().ToList();
        var known = clientIds.Count == 0
            ? []
            : (await db.LocationPings.AsNoTracking()
                .Where(p => p.RepresentativeId == representativeId && p.ClientId != null && clientIds.Contains(p.ClientId.Value))
                .Select(p => p.ClientId!.Value)
                .ToListAsync(ct)).ToHashSet();

        var accepted = 0;
        foreach (var point in points)
        {
            if (point.ClientId is { } clientId && !known.Add(clientId)) continue;

            db.LocationPings.Add(new LocationPing
            {
                RepresentativeId = representativeId,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                TimestampUtc = point.TimestampUtc is { } ts ? DateTime.SpecifyKind(ts, DateTimeKind.Utc) : receivedAtUtc,
                AccuracyMeters = point.AccuracyMeters,
                AltitudeMeters = point.AltitudeMeters,
                SpeedMps = point.SpeedMps,
                Heading = point.Heading,
                Source = point.Source ?? LocationPingSources.Track,
                IsAnomaly = point.IsAnomaly,
                ClientId = point.ClientId,
                ReceivedAtUtc = receivedAtUtc
            });
            accepted++;
        }

        if (accepted > 0) await db.SaveChangesAsync(ct);
        return new LocationPingBatchResult(accepted, points.Count - accepted);
    }

    private static void Validate(LocationPingRequest p, int index, DateTime receivedAtUtc)
    {
        var where = $"Point {index + 1}";
        if (double.IsNaN(p.Latitude) || p.Latitude is < -90 or > 90)
            throw new ValidationFailedException($"{where}: latitude must be between -90 and 90.");
        if (double.IsNaN(p.Longitude) || p.Longitude is < -180 or > 180)
            throw new ValidationFailedException($"{where}: longitude must be between -180 and 180.");
        if (p.AccuracyMeters is < 0)
            throw new ValidationFailedException($"{where}: accuracy cannot be negative.");
        if (p.Source is { } source && !LocationPingSources.All.Contains(source))
            throw new ValidationFailedException($"{where}: source must be one of {string.Join(", ", LocationPingSources.All)}.");
        if (p.TimestampUtc is { } ts)
        {
            if (ts > receivedAtUtc + MaxFutureSkew)
                throw new ValidationFailedException($"{where}: timestamp is in the future — check the device clock.");
            if (ts < receivedAtUtc - MaxAge)
                throw new ValidationFailedException($"{where}: timestamp is too old to be recorded.");
        }
    }

    public async Task<IReadOnlyList<RepresentativeLocationDto>> GetLatestLocationsAsync(int? territoryId,
        CancellationToken ct = default)
    {
        var todayStartUtc = calendar.StartOfDayUtc(calendar.Today);

        var repsQuery = db.Representatives.AsNoTracking().Where(r => !r.IsDeleted);
        if (territoryId.HasValue) repsQuery = repsQuery.Where(r => r.TerritoryId == territoryId);
        var repIds = await repsQuery.Select(r => r.Id).ToListAsync(ct);

        // GroupBy+First doesn't translate cleanly through the record projection below, so materialize
        // the raw rows first and do the "latest per representative" reduction in memory.
        var pings = await db.LocationPings.AsNoTracking()
            .Where(p => repIds.Contains(p.RepresentativeId) && p.TimestampUtc >= todayStartUtc)
            .Select(p => new RepresentativeLocationDto(p.RepresentativeId, p.Representative.FullName,
                p.Latitude, p.Longitude, p.TimestampUtc, "Ping"))
            .ToListAsync(ct);

        var checkIns = await db.DoctorVisits.AsNoTracking()
            .Where(v => repIds.Contains(v.RepresentativeId) && v.VisitDateUtc >= todayStartUtc
                        && v.CheckInLatitude != null && v.CheckInLongitude != null)
            .Select(v => new RepresentativeLocationDto(v.RepresentativeId, v.Representative.FullName,
                v.CheckInLatitude!.Value, v.CheckInLongitude!.Value, v.VisitDateUtc, "VisitCheckIn"))
            .ToListAsync(ct);

        return pings.Concat(checkIns)
            .GroupBy(l => l.RepresentativeId)
            .Select(g => g.OrderByDescending(l => l.TimestampUtc).First())
            .OrderBy(l => l.RepresentativeName)
            .ToList();
    }

    public async Task<IReadOnlyList<RepresentativeLocationDto>> GetRepresentativeTrailAsync(int representativeId,
        DateOnly date, CancellationToken ct = default)
    {
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = date.ToDateTime(TimeOnly.MaxValue);

        var pings = await db.LocationPings.AsNoTracking()
            .Where(p => p.RepresentativeId == representativeId && p.TimestampUtc >= dayStartUtc && p.TimestampUtc <= dayEndUtc)
            .Select(p => new RepresentativeLocationDto(p.RepresentativeId, p.Representative.FullName,
                p.Latitude, p.Longitude, p.TimestampUtc, "Ping"))
            .ToListAsync(ct);

        var checkIns = await db.DoctorVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == representativeId && v.VisitDateUtc >= dayStartUtc && v.VisitDateUtc <= dayEndUtc
                        && v.CheckInLatitude != null && v.CheckInLongitude != null)
            .Select(v => new RepresentativeLocationDto(v.RepresentativeId, v.Representative.FullName,
                v.CheckInLatitude!.Value, v.CheckInLongitude!.Value, v.VisitDateUtc, "VisitCheckIn"))
            .ToListAsync(ct);

        return pings.Concat(checkIns).OrderBy(l => l.TimestampUtc).ToList();
    }
}

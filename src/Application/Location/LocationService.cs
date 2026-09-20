using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Location;

public class LocationService(IAppDbContext db) : ILocationService
{
    public async Task RecordPingAsync(int representativeId, LocationPingRequest request, CancellationToken ct = default)
    {
        db.LocationPings.Add(new LocationPing
        {
            RepresentativeId = representativeId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TimestampUtc = request.TimestampUtc ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RepresentativeLocationDto>> GetLatestLocationsAsync(int? territoryId,
        CancellationToken ct = default)
    {
        var todayStartUtc = DateTime.UtcNow.Date;

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

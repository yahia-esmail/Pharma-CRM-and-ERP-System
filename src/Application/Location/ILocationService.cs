namespace PharmaERP.Application.Location;

public interface ILocationService
{
    Task RecordPingAsync(int representativeId, LocationPingRequest request, CancellationToken ct = default);

    /// <summary>Latest known point per representative today — pings and visit check-ins, whichever is most recent (spec 4.10 manager map view).</summary>
    Task<IReadOnlyList<RepresentativeLocationDto>> GetLatestLocationsAsync(int? territoryId, CancellationToken ct = default);

    /// <summary>Every recorded point (pings + visit check-ins) for one representative on one day, in time order — for a route view.</summary>
    Task<IReadOnlyList<RepresentativeLocationDto>> GetRepresentativeTrailAsync(int representativeId, DateOnly date, CancellationToken ct = default);
}

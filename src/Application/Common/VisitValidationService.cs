using Microsoft.Extensions.Options;
using PharmaERP.Application.Territories;

namespace PharmaERP.Application.Common;

public class VisitValidationService(ITerritoryService territoryService, IOptions<VisitValidationOptions> options) : IVisitValidationService
{
    private const double EarthRadiusMeters = 6_371_000;

    public async Task<VisitValidationResult> EvaluateAsync(int? representativeTerritoryId, int? customerTerritoryId,
        double? customerLatitude, double? customerLongitude, double? checkInLatitude, double? checkInLongitude,
        int? durationMinutes, CancellationToken ct = default)
    {
        return new VisitValidationResult(
            IsLocationMismatch(customerLatitude, customerLongitude, checkInLatitude, checkInLongitude, accuracyMeters: null),
            await IsOutsideTerritoryAsync(representativeTerritoryId, customerTerritoryId, ct),
            IsDurationTooShort(durationMinutes));
    }

    public bool IsLocationMismatch(double? customerLatitude, double? customerLongitude,
        double? latitude, double? longitude, double? accuracyMeters)
    {
        // Location match: only evaluable when both the customer's registered coordinates and the
        // check-in coordinates are present — otherwise there's nothing to compare, so no flag is raised.
        if (customerLatitude is not { } custLat || customerLongitude is not { } custLon
            || latitude is not { } lat || longitude is not { } lon)
            return false;

        var settings = options.Value;
        var credit = Math.Clamp(accuracyMeters ?? 0, 0, settings.MaxCreditedAccuracyMeters);
        return DistanceMeters(custLat, custLon, lat, lon) - credit > settings.GeofenceRadiusMeters;
    }

    public async Task<bool> IsOutsideTerritoryAsync(int? representativeTerritoryId, int? customerTerritoryId,
        CancellationToken ct = default)
    {
        // Territory match: is the customer's territory the representative's own territory, or beneath it
        // in the hierarchy? Only evaluable when both sides have a territory assigned.
        if (representativeTerritoryId is not { } repTerritoryId || customerTerritoryId is not { } custTerritoryId)
            return false;

        var repSubtree = await territoryService.GetDescendantTerritoryIdsAsync(repTerritoryId, ct);
        return !repSubtree.Contains(custTerritoryId);
    }

    public bool IsDurationTooShort(int? durationMinutes) =>
        durationMinutes is { } minutes && minutes < options.Value.MinimumVisitDurationMinutes;

    /// <summary>Haversine great-circle distance between two lat/long points, in meters.</summary>
    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

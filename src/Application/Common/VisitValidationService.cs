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
        var settings = options.Value;

        // Location match: only evaluable when both the customer's registered coordinates and the
        // check-in coordinates are present — otherwise there's nothing to compare, so no flag is raised.
        var locationMismatch = false;
        if (customerLatitude is { } custLat && customerLongitude is { } custLon
            && checkInLatitude is { } inLat && checkInLongitude is { } inLon)
        {
            var distance = DistanceMeters(custLat, custLon, inLat, inLon);
            locationMismatch = distance > settings.GeofenceRadiusMeters;
        }

        // Territory match: is the customer's territory the representative's own territory, or beneath it
        // in the hierarchy? Only evaluable when both sides have a territory assigned.
        var outsideTerritory = false;
        if (representativeTerritoryId is { } repTerritoryId && customerTerritoryId is { } custTerritoryId)
        {
            var repSubtree = await territoryService.GetDescendantTerritoryIdsAsync(repTerritoryId, ct);
            outsideTerritory = !repSubtree.Contains(custTerritoryId);
        }

        var durationTooShort = durationMinutes is { } minutes && minutes < settings.MinimumVisitDurationMinutes;

        return new VisitValidationResult(locationMismatch, outsideTerritory, durationTooShort);
    }

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

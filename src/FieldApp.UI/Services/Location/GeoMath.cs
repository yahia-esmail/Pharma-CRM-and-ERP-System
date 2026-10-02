namespace PharmaERP.FieldApp.UI.Services.Location;

/// <summary>Great-circle geometry on a spherical Earth — accurate to well under 1% at city distances,
/// which is far below GPS error.</summary>
public static class GeoMath
{
    public const double EarthRadiusMeters = 6_371_000;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double DistanceMeters(GeoFix a, GeoFix b) =>
        DistanceMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

    /// <summary>Initial compass bearing from point 1 to point 2, 0–360° (0 = north).</summary>
    public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = ToRadians(lat1);
        var φ2 = ToRadians(lat2);
        var Δλ = ToRadians(lon2 - lon1);
        var y = Math.Sin(Δλ) * Math.Cos(φ2);
        var x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>Straight-line length of a path through the points in order (the "Route 14.2 km"
    /// estimate on Today's Plan — road distance will be longer).</summary>
    public static double PathLengthMeters(IEnumerable<(double Latitude, double Longitude)> points)
    {
        var total = 0d;
        (double Latitude, double Longitude)? previous = null;
        foreach (var point in points)
        {
            if (previous is { } p) total += DistanceMeters(p.Latitude, p.Longitude, point.Latitude, point.Longitude);
            previous = point;
        }
        return total;
    }

    public static string FormatDistance(double meters) =>
        meters < 1000 ? $"{Math.Round(meters / 10) * 10:0} m" : $"{meters / 1000:0.0} km";

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

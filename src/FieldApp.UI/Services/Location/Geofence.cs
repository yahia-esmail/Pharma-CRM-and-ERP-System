namespace PharmaERP.FieldApp.UI.Services.Location;

public enum GeofenceStatus
{
    /// <summary>The target has no coordinates yet (plan 7.9) — offer to capture them.</summary>
    NoTargetLocation,
    /// <summary>Inside the radius even allowing for the fix's error.</summary>
    Inside,
    /// <summary>The error circle straddles the boundary — can't tell; better accuracy would settle it.</summary>
    Uncertain,
    /// <summary>Outside the radius even allowing for the fix's error.</summary>
    Outside
}

public sealed record GeofenceResult(GeofenceStatus Status, double? DistanceMeters, double RadiusMeters)
{
    public string Describe() => Status switch
    {
        GeofenceStatus.NoTargetLocation => "Location not set for this customer",
        GeofenceStatus.Inside => $"Within {RadiusMeters:0} m radius",
        GeofenceStatus.Uncertain => $"About {GeoMath.FormatDistance(DistanceMeters!.Value)} away — GPS accuracy too low to confirm",
        _ => $"{GeoMath.FormatDistance(DistanceMeters!.Value)} away (limit {RadiusMeters:0} m)"
    };
}

/// <summary>Device-side geofence check — for the rep's feedback only; the server makes the binding
/// decision (plan 7.6). Unlike a bare "distance ≤ radius", it accounts for the fix's accuracy: a reading
/// ±80 m that lands 150 m from a 200 m fence could really be at 230 m.</summary>
public static class Geofence
{
    public static GeofenceResult Evaluate(GeoFix fix, double? targetLatitude, double? targetLongitude, double radiusMeters)
    {
        if (targetLatitude is not { } lat || targetLongitude is not { } lon)
            return new GeofenceResult(GeofenceStatus.NoTargetLocation, null, radiusMeters);

        var distance = GeoMath.DistanceMeters(fix.Latitude, fix.Longitude, lat, lon);
        var status = distance + fix.AccuracyMeters <= radiusMeters ? GeofenceStatus.Inside
            : distance - fix.AccuracyMeters > radiusMeters ? GeofenceStatus.Outside
            : GeofenceStatus.Uncertain;
        return new GeofenceResult(status, distance, radiusMeters);
    }
}

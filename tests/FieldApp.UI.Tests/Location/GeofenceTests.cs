using PharmaERP.FieldApp.UI.Services.Location;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;

namespace PharmaERP.FieldApp.UI.Tests.Location;

public class GeofenceTests
{
    private const double Radius = 200;

    [Fact]
    public void Accurate_fix_near_the_pharmacy_is_inside()
    {
        var result = Geofence.Evaluate(Fix(north: 50, accuracy: 10), Lat, Lon, Radius);
        Assert.Equal(GeofenceStatus.Inside, result.Status);
        Assert.InRange(result.DistanceMeters!.Value, 49, 51);
    }

    [Fact]
    public void Accurate_fix_far_away_is_outside()
    {
        Assert.Equal(GeofenceStatus.Outside, Geofence.Evaluate(Fix(north: 400, accuracy: 15), Lat, Lon, Radius).Status);
    }

    [Fact]
    public void Inaccurate_fix_that_lands_inside_is_only_uncertain()
    {
        // 150 m ±80 m: the rep could really be 230 m away, past the 200 m fence.
        Assert.Equal(GeofenceStatus.Uncertain, Geofence.Evaluate(Fix(north: 150, accuracy: 80), Lat, Lon, Radius).Status);
    }

    [Fact]
    public void Inaccurate_fix_that_lands_just_outside_is_only_uncertain()
    {
        Assert.Equal(GeofenceStatus.Uncertain, Geofence.Evaluate(Fix(north: 240, accuracy: 80), Lat, Lon, Radius).Status);
    }

    [Fact]
    public void Customer_without_coordinates_is_reported_as_such()
    {
        var result = Geofence.Evaluate(Fix(), null, null, Radius);
        Assert.Equal(GeofenceStatus.NoTargetLocation, result.Status);
        Assert.Null(result.DistanceMeters);
    }

    [Fact]
    public void Description_tells_the_rep_how_far_away_they_are()
    {
        Assert.Equal("Within 200 m radius", Geofence.Evaluate(Fix(north: 50), Lat, Lon, Radius).Describe());
        Assert.Equal("400 m away (limit 200 m)", Geofence.Evaluate(Fix(north: 400), Lat, Lon, Radius).Describe());
    }
}

using PharmaERP.FieldApp.UI.Services.Location;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;

namespace PharmaERP.FieldApp.UI.Tests.Location;

public class GeoMathTests
{
    [Fact]
    public void Distance_between_Cairo_and_Alexandria_matches_the_known_value()
    {
        // Tahrir Square → Alexandria Library: ~180 km great-circle.
        var d = GeoMath.DistanceMeters(30.0444, 31.2357, 31.2089, 29.9092);
        Assert.InRange(d, 178_000, 182_000);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 250)]
    [InlineData(30, 40)]
    public void Short_offsets_are_measured_to_within_a_metre(double north, double east)
    {
        var d = GeoMath.DistanceMeters(Fix(), Fix(north, east));
        Assert.InRange(d, Math.Sqrt(north * north + east * east) - 1, Math.Sqrt(north * north + east * east) + 1);
    }

    [Fact]
    public void Distance_to_the_same_point_is_zero()
    {
        Assert.Equal(0, GeoMath.DistanceMeters(Lat, Lon, Lat, Lon), 6);
    }

    [Theory]
    [InlineData(100, 0, 0)]
    [InlineData(0, 100, 90)]
    [InlineData(-100, 0, 180)]
    [InlineData(0, -100, 270)]
    public void Bearing_points_the_right_way(double north, double east, double expected)
    {
        var target = Fix(north, east);
        Assert.InRange(GeoMath.BearingDegrees(Lat, Lon, target.Latitude, target.Longitude), expected - 0.5, expected + 0.5);
    }

    [Fact]
    public void Path_length_sums_the_legs_in_order()
    {
        var points = new[] { Fix(), Fix(300), Fix(300, 400) }.Select(f => (f.Latitude, f.Longitude));
        Assert.InRange(GeoMath.PathLengthMeters(points), 698, 702);
    }

    [Theory]
    [InlineData(348, "350 m")]
    [InlineData(1234, "1.2 km")]
    [InlineData(14_210, "14.2 km")]
    public void Distances_are_formatted_for_the_rep(double meters, string expected)
    {
        Assert.Equal(expected, GeoMath.FormatDistance(meters));
    }
}

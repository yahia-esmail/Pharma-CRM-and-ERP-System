using PharmaERP.FieldApp.UI.Services.Location;

namespace PharmaERP.FieldApp.UI.Tests.Location;

internal static class GeoTestData
{
    // Tahrir Square, Cairo.
    public const double Lat = 30.0444;
    public const double Lon = 31.2357;

    public static readonly DateTime T0 = new(2026, 9, 29, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A fix <paramref name="north"/> north / <paramref name="east"/> east of the base point.</summary>
    public static GeoFix Fix(double north = 0, double east = 0, double accuracy = 10, double seconds = 0,
        GeoFixSource source = GeoFixSource.Track) =>
        new(Lat + north / 111_195.0,
            Lon + east / (111_195.0 * Math.Cos(Lat * Math.PI / 180)),
            accuracy, null, null, null, T0.AddSeconds(seconds), 1, 0, source);
}

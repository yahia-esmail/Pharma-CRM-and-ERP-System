namespace PharmaERP.FieldApp.UI.Services;

/// <summary>Bound from the "Api" section of wwwroot/appsettings.json. That file is downloaded by the
/// browser, so it must never hold secrets.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "";
}

/// <summary>Map tiles (plan 7.10). The OpenStreetMap default is fine for development and a pilot, but
/// OSM's public servers are not meant for a commercial fleet — point this at a provider (MapTiler,
/// Stadia…) or a self-hosted tile server before go-live.</summary>
public sealed class MapOptions
{
    public const string SectionName = "Map";

    public string TileUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public string Attribution { get; set; } = "&copy; OpenStreetMap contributors";
}

/// <summary>GPS tuning knobs (plan section 7). Defaults here mirror appsettings.json; they are expected
/// to move to a server-side settings endpoint so management can tune them after the pilot.</summary>
public sealed class GpsOptions
{
    public const string SectionName = "Gps";

    public double TargetAccuracyMeters { get; set; } = 20;
    public int MaxWaitMs { get; set; } = 15000;
    public int LowAccuracyOfferAfterMs { get; set; } = 20000;
    public double MaxAcceptedAccuracyMeters { get; set; } = 100;
    public double TrackDistanceMeters { get; set; } = 50;
    public int TrackIntervalSeconds { get; set; } = 180;
    public double MaxSpeedKmh { get; set; } = 150;
    public int UploadBatchSize { get; set; } = 20;
    public int UploadIntervalSeconds { get; set; } = 120;
    public double GeofenceRadiusDoctorMeters { get; set; } = 200;
    public double GeofenceRadiusPharmacyMeters { get; set; } = 200;
    public TimeOnly WorkingHoursStart { get; set; } = new(8, 0);
    public TimeOnly WorkingHoursEnd { get; set; } = new(20, 0);
}

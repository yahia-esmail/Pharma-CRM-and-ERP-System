namespace PharmaERP.Application.Location;

/// <summary>One GPS fix from the field app. Only latitude/longitude are required so older clients keep
/// working; current clients always send accuracy, source and a client id.</summary>
public class LocationPingRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Device time of the fix. Defaults to server receipt time when omitted.</summary>
    public DateTime? TimestampUtc { get; set; }

    public double? AccuracyMeters { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SpeedMps { get; set; }
    public double? Heading { get; set; }

    /// <summary>Event, Track or Route (defaults to Track).</summary>
    public string? Source { get; set; }

    public bool IsAnomaly { get; set; }

    /// <summary>Makes re-sending the same point harmless: a known client id is skipped.</summary>
    public Guid? ClientId { get; set; }
}

/// <summary>Points buffered on the device (offline, or between upload intervals) sent in one request.</summary>
public class LocationPingBatchRequest
{
    public const int MaxPoints = 500;

    public List<LocationPingRequest> Points { get; set; } = [];
}

/// <param name="Accepted">Points stored.</param>
/// <param name="Duplicates">Points skipped because their client id was already stored.</param>
public record LocationPingBatchResult(int Accepted, int Duplicates);

public record RepresentativeLocationDto(
    int RepresentativeId,
    string RepresentativeName,
    double Latitude,
    double Longitude,
    DateTime TimestampUtc,
    string Source);

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Optional periodic GPS ping during working hours for route verification (spec 4.10). Deliberately
/// not an AuditableEntity — high-volume, append-only telemetry, not a business transaction that needs
/// full audit-log tracing; still retained (soft cleanup only) for the manager map view (spec 4.2).
/// </summary>
public class LocationPing
{
    public long Id { get; set; }
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    /// <summary>When the device took the fix (device clock — compare with <see cref="ReceivedAtUtc"/>).</summary>
    public DateTime TimestampUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Radius of 68% confidence reported by the device, in metres. Null for pings from app
    /// versions that predate accuracy reporting.</summary>
    public double? AccuracyMeters { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SpeedMps { get; set; }
    public double? Heading { get; set; }

    /// <summary>Why the fix was taken: Event (check-in, order…), Track (foreground tracking) or Route
    /// (route mode with the screen kept on).</summary>
    public string Source { get; set; } = LocationPingSources.Track;

    /// <summary>Flagged on the device by its jump filter (implied speed impossible within a city). Kept
    /// rather than dropped: repeated anomalies are a spoofing signal (plan 7.8).</summary>
    public bool IsAnomaly { get; set; }

    /// <summary>Client-generated id, so a batch re-sent after a lost response doesn't store points twice.</summary>
    public Guid? ClientId { get; set; }

    /// <summary>Server clock at receipt. A large gap to <see cref="TimestampUtc"/> means either the point
    /// was queued offline (normal) or the device clock is wrong (suspicious if the fix is "in the future").</summary>
    public DateTime ReceivedAtUtc { get; set; }
}

public static class LocationPingSources
{
    public const string Event = "Event";
    public const string Track = "Track";
    public const string Route = "Route";

    public static readonly IReadOnlySet<string> All = new HashSet<string>([Event, Track, Route]);
}

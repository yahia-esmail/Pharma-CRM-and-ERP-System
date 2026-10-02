using System.Text.Json.Serialization;

namespace PharmaERP.FieldApp.UI.Services.Location;

/// <summary>Why a fix was taken — sent to the API as <c>source</c>. Serialized as a string both ways
/// (geo.js produces "Event"/"Track"/"Route"; Blazor's interop serializer has no enum converter).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GeoFixSource>))]
public enum GeoFixSource { Event, Track, Route }

/// <summary>≤10 m, ≤30 m, ≤100 m, worse (plan 7.11).</summary>
public enum GeoFixQuality { Excellent, Good, Fair, Poor }

public enum GeoPermission { Unknown, Prompt, Granted, Denied, Unsupported }

public enum GeoError { None, Unsupported, Denied, Unavailable, Timeout, Insecure }

/// <summary>One position reading, as produced by geo.js.</summary>
public sealed record GeoFix(
    double Latitude,
    double Longitude,
    double AccuracyMeters,
    double? AltitudeMeters,
    double? SpeedMps,
    double? Heading,
    DateTime DeviceTimestampUtc,
    int SamplesCollected,
    int ElapsedMs,
    GeoFixSource Source)
{
    public GeoFixQuality Quality => AccuracyMeters switch
    {
        <= 10 => GeoFixQuality.Excellent,
        <= 30 => GeoFixQuality.Good,
        <= 100 => GeoFixQuality.Fair,
        _ => GeoFixQuality.Poor
    };
}

public sealed record GeoFixResult(GeoFix? Fix, GeoError Error, string? Message)
{
    public bool Succeeded => Fix is not null;

    public static GeoFixResult Success(GeoFix fix) => new(fix, GeoError.None, null);

    public static GeoFixResult Failure(GeoError error, string? message) => new(null, error, message);

    /// <summary>What to tell the rep, per failure type (plan 7.7).</summary>
    public string? UserMessage => Error switch
    {
        GeoError.None => null,
        GeoError.Denied => "Location permission is turned off for this app.",
        GeoError.Unavailable => "Your phone can't find its location. Turn on Location (GPS) in the phone settings.",
        GeoError.Timeout => "Couldn't get a location fix in time. Move near a window or outdoors and try again.",
        GeoError.Insecure => "Location only works over a secure (https) connection.",
        _ => "This browser doesn't support location."
    };
}

namespace PharmaERP.Application.Common;

/// <summary>Company-policy thresholds for the Visit Validation engine (gap-analysis addendum 3.3).
/// Bound from configuration ("VisitValidation" section) — these defaults apply if that section is absent.</summary>
public class VisitValidationOptions
{
    /// <summary>Maximum acceptable distance, in meters, between the check-in GPS point and the
    /// customer's registered location before a visit is flagged as a location mismatch.</summary>
    public double GeofenceRadiusMeters { get; set; } = 200;

    /// <summary>A visit shorter than this is flagged as suspiciously brief.</summary>
    public int MinimumVisitDurationMinutes { get; set; } = 2;

    /// <summary>At most this much of a fix's reported accuracy is credited to the rep in the geofence
    /// check — a ±900 m cell-tower fix must not make any check-in "match".</summary>
    public double MaxCreditedAccuracyMeters { get; set; } = 100;

    /// <summary>Device clock offset (vs. the server, measured on each request) beyond which a visit's
    /// device times are treated as suspect.</summary>
    public int MaxDeviceClockOffsetMinutes { get; set; } = 5;

    /// <summary>A change in the device clock offset between check-in and check-out beyond this means the
    /// clock was adjusted during the visit — which would distort its duration.</summary>
    public int MaxClockDriftDuringVisitSeconds { get; set; } = 120;

    /// <summary>A customer's first location is applied without review when the fix is at least this accurate
    /// (plan 7.9); less accurate fixes, and any change to an existing location, wait for a manager.</summary>
    public double AutoApplyLocationMaxAccuracyMeters { get; set; } = 30;
}

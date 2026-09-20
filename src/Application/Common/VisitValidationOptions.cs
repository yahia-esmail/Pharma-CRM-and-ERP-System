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
}

using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Visits;

/// <summary>A GPS fix as the field app captured it (Best-of-N, see plan 7.3).</summary>
public class VisitFix
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime? DeviceTimestampUtc { get; set; }
}

/// <summary>Starts a visit. <see cref="DeviceTimeUtc"/> is when the rep tapped Check-in, on the device
/// clock; the server corrects it using the X-Client-Sent-At header (see IFieldVisit).</summary>
public class VisitCheckInRequest
{
    public int? VisitPlanItemId { get; set; }
    public DateTime DeviceTimeUtc { get; set; }

    /// <summary>Null when location was unavailable or denied — the visit is still recorded.</summary>
    public VisitFix? Location { get; set; }

    /// <summary>Required by the app when it showed the rep outside the geofence ("Doctor moved clinic"…).</summary>
    public string? OutsideGeofenceReason { get; set; }
}

public class DoctorVisitCheckOutRequest
{
    public DateTime DeviceTimeUtc { get; set; }
    public VisitFix? Location { get; set; }

    public string? ProductsDiscussed { get; set; }
    public string? SamplesGiven { get; set; }
    public string? MaterialsLeft { get; set; }
    public string? FeedbackNotes { get; set; }
    public string? NextVisitRecommendation { get; set; }
    public VisitInterestLevel? InterestLevel { get; set; }
}

public class PharmacyVisitCheckOutRequest
{
    public DateTime DeviceTimeUtc { get; set; }
    public VisitFix? Location { get; set; }

    public PharmacyVisitPurpose Purpose { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Server verdict at check-in — the app replaces its own provisional badges with these.</summary>
public record VisitCheckInResult(int Id, DateTime CheckInUtc, bool IsPlanned, int? VisitPlanItemId,
    bool LocationMismatch, bool OutsideTerritory, bool DeviceClockSuspect);

public record VisitCheckOutResult(int Id, DateTime CheckInUtc, int DurationMinutes, bool LocationMismatch,
    bool OutsideTerritory, bool DurationTooShort, bool DeviceClockSuspect);

// ---- Customer location proposals (plan 7.9) -------------------------------------------------------

public class LocationProposalRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AccuracyMeters { get; set; }
    public DateTime? CapturedAtUtc { get; set; }
    public string? Note { get; set; }
}

public record LocationProposalResult(int Id, LocationProposalStatus Status);

public record LocationProposalDto(
    int Id,
    string CustomerKind,
    int CustomerId,
    string CustomerName,
    int RepresentativeId,
    string RepresentativeName,
    double Latitude,
    double Longitude,
    double AccuracyMeters,
    DateTime CapturedAtUtc,
    double? PreviousLatitude,
    double? PreviousLongitude,
    double? MoveDistanceMeters,
    string? Note,
    LocationProposalStatus Status,
    DateTime CreatedAtUtc);

public class LocationProposalReviewRequest
{
    public string? Note { get; set; }
}

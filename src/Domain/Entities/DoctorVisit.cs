using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class DoctorVisit : AuditableEntity, IFieldVisit
{
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime VisitDateUtc { get; set; }
    public int? DurationMinutes { get; set; }

    public string? ProductsDiscussed { get; set; }
    public string? SamplesGiven { get; set; }
    public string? MaterialsLeft { get; set; }
    public string? FeedbackNotes { get; set; }
    public string? NextVisitRecommendation { get; set; }

    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>Set once the record passes the configurable edit window (see 4.1) — further changes must be captured as amendments.</summary>
    public bool IsLocked { get; set; }

    /// <summary>The planned stop this visit fulfills, when the client links it explicitly at check-in
    /// (mobile "Today's Plan" flow); null for an unplanned visit or one logged before this field existed,
    /// in which case IsPlanned falls back to a date/doctor match against Approved plans.</summary>
    public int? VisitPlanItemId { get; set; }

    // Visit Validation engine (addendum 3.3) — set once at check-in time, never re-evaluated afterward.
    /// <summary>True when a VisitPlanItem exists for this doctor/representative/date in an Approved plan.</summary>
    public bool IsPlanned { get; set; }
    public bool LocationMismatch { get; set; }
    public bool OutsideTerritory { get; set; }
    public bool DurationTooShort { get; set; }

    public VisitInterestLevel? InterestLevel { get; set; }

    // Mobile check-in → check-out flow (IFieldVisit). Legacy single-request visits leave these null and
    // read as Completed.
    public VisitSessionStatus SessionStatus { get; set; }
    public double? CheckInAccuracyMeters { get; set; }
    public DateTime? CheckInDeviceTimeUtc { get; set; }
    public DateTime? CheckInReceivedAtUtc { get; set; }
    public double? CheckInClockOffsetSeconds { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public double? CheckOutAccuracyMeters { get; set; }
    public DateTime? CheckOutDeviceTimeUtc { get; set; }
    public DateTime? CheckOutReceivedAtUtc { get; set; }
    public double? CheckOutClockOffsetSeconds { get; set; }
    public string? OutsideGeofenceReason { get; set; }
    public bool DeviceClockSuspect { get; set; }
}

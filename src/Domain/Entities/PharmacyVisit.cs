using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class PharmacyVisit : AuditableEntity, IFieldVisit
{
    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime VisitDateUtc { get; set; }
    public int? DurationMinutes { get; set; }
    public PharmacyVisitPurpose Purpose { get; set; }
    public string? Notes { get; set; }

    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }

    /// <summary>The planned stop this visit fulfills, when the client links it explicitly at check-in
    /// (mobile "Today's Plan" flow); null for an unplanned visit or one logged before this field existed,
    /// in which case IsPlanned falls back to a date/pharmacy match against Approved plans.</summary>
    public int? VisitPlanItemId { get; set; }

    /// <summary>True when a VisitPlanItem exists for this pharmacy/representative/date in an Approved plan
    /// — mirrors DoctorVisit.IsPlanned, now that VisitPlanItem supports pharmacy stops too.</summary>
    public bool IsPlanned { get; set; }

    // Visit Validation engine (addendum 3.3)
    public bool LocationMismatch { get; set; }
    public bool OutsideTerritory { get; set; }
    public bool DurationTooShort { get; set; }

    // Mobile check-in → check-out flow (IFieldVisit). Legacy single-request visits leave these null and
    // read as Completed.
    public VisitSessionStatus SessionStatus { get; set; }
    public double? CheckInAccuracyMeters { get; set; }
    public int? CheckInFixElapsedMs { get; set; }
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

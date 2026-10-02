using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Common;

/// <summary>What doctor and pharmacy visits share for the mobile check-in → check-out flow (plan 6.1 item 6),
/// so one service can time and validate both.
///
/// Timing: the device reports when things happened on <i>its</i> clock, and with every request the time it
/// was sent (X-Client-Sent-At). The server measures the device clock's offset at each request and corrects
/// the device times with it — so a visit recorded offline and uploaded hours later still gets its real
/// check-in time and duration, while a device clock that is wrong (or was changed mid-visit) is detected.</summary>
public interface IFieldVisit
{
    int RepresentativeId { get; set; }
    DateTime VisitDateUtc { get; set; }
    int? DurationMinutes { get; set; }
    VisitSessionStatus SessionStatus { get; set; }

    int? VisitPlanItemId { get; set; }
    bool IsPlanned { get; set; }

    double? CheckInLatitude { get; set; }
    double? CheckInLongitude { get; set; }
    double? CheckInAccuracyMeters { get; set; }

    /// <summary>How long the phone took to get the check-in fix (pilot KPI, plan 7.1: ≤ 10 s in 90 % of cases).</summary>
    int? CheckInFixElapsedMs { get; set; }
    DateTime? CheckInDeviceTimeUtc { get; set; }
    DateTime? CheckInReceivedAtUtc { get; set; }
    double? CheckInClockOffsetSeconds { get; set; }

    double? CheckOutLatitude { get; set; }
    double? CheckOutLongitude { get; set; }
    double? CheckOutAccuracyMeters { get; set; }
    DateTime? CheckOutDeviceTimeUtc { get; set; }
    DateTime? CheckOutReceivedAtUtc { get; set; }
    double? CheckOutClockOffsetSeconds { get; set; }

    /// <summary>Why the rep checked in although the app showed them outside the geofence.</summary>
    string? OutsideGeofenceReason { get; set; }

    bool LocationMismatch { get; set; }
    bool OutsideTerritory { get; set; }
    bool DurationTooShort { get; set; }

    /// <summary>Device clock far off the server's, or changed between check-in and check-out.</summary>
    bool DeviceClockSuspect { get; set; }

    bool IsDeleted { get; set; }
}

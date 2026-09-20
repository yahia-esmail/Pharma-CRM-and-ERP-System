using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Pharmacies;

public record PharmacyVisitDto(
    int Id,
    int PharmacyId,
    int RepresentativeId,
    string RepresentativeName,
    DateTime VisitDateUtc,
    int? DurationMinutes,
    PharmacyVisitPurpose Purpose,
    string? Notes,
    double? CheckInLatitude,
    double? CheckInLongitude,
    int? VisitPlanItemId,
    bool IsPlanned,
    bool LocationMismatch,
    bool OutsideTerritory,
    bool DurationTooShort);

public class PharmacyVisitSaveRequest
{
    public int PharmacyId { get; set; }
    public PharmacyVisitPurpose Purpose { get; set; }
    public DateTime VisitDateUtc { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }

    /// <summary>Optional explicit link to the plan stop this visit fulfills (mobile "Today's Plan" flow) —
    /// when omitted, IsPlanned is inferred by matching pharmacy/representative/date against Approved plans.</summary>
    public int? VisitPlanItemId { get; set; }
}

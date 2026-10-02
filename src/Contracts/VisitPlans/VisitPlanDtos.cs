using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.VisitPlans;

public record VisitPlanListItemDto(
    int Id,
    int RepresentativeId,
    string RepresentativeName,
    VisitPlanPeriodType PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    VisitPlanStatus Status,
    int ItemCount);

public record VisitPlanItemDto(
    int Id,
    int? DoctorId,
    string? DoctorName,
    int? PharmacyId,
    string? PharmacyName,
    DateOnly PlannedDate,
    int Sequence,
    string? Notes,
    // Position of the doctor/pharmacy for this stop (null when not captured yet), so Today's Plan can
    // draw its map and distances without one extra request per stop.
    double? Latitude = null,
    double? Longitude = null,
    // Card details for Today's Plan: doctor specialty / pharmacy segment, classification, where to go.
    string? Specialty = null,
    string? ClassificationName = null,
    string? Address = null,
    string? City = null,
    // When a visit linked to this stop was recorded (null = not visited yet). Filled by the "today"
    // endpoint only — plan detail views don't need it.
    DateTime? VisitedAtUtc = null);

public record VisitPlanDetailDto(
    int Id,
    int RepresentativeId,
    string RepresentativeName,
    VisitPlanPeriodType PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    VisitPlanStatus Status,
    DateTime? SubmittedAtUtc,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    string? RejectionReason,
    IReadOnlyList<VisitPlanItemDto> Items);

public class VisitPlanCreateRequest
{
    public int RepresentativeId { get; set; }
    public VisitPlanPeriodType PeriodType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class VisitPlanItemSaveRequest
{
    public int? DoctorId { get; set; }
    public int? PharmacyId { get; set; }
    public DateOnly PlannedDate { get; set; }
    public int Sequence { get; set; }
    public string? Notes { get; set; }
}

public record VisitPlanVarianceItemDto(
    int? DoctorId,
    string? DoctorName,
    int? PharmacyId,
    string? PharmacyName,
    DateOnly PlannedDate,
    bool Visited,
    DateTime? ActualVisitDateUtc);

public record PlanVsActualDto(
    int VisitPlanId,
    int PlannedCount,
    int VisitedCount,
    double CoveragePercent,
    IReadOnlyList<VisitPlanVarianceItemDto> Items);

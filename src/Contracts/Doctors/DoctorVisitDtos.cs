using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Doctors;

public record DoctorVisitDto(
    int Id,
    int DoctorId,
    int RepresentativeId,
    string RepresentativeName,
    DateTime VisitDateUtc,
    int? DurationMinutes,
    string? ProductsDiscussed,
    string? SamplesGiven,
    string? MaterialsLeft,
    string? FeedbackNotes,
    string? NextVisitRecommendation,
    double? CheckInLatitude,
    double? CheckInLongitude,
    bool IsLocked,
    int? VisitPlanItemId,
    bool IsPlanned,
    bool LocationMismatch,
    bool OutsideTerritory,
    bool DurationTooShort);

public class DoctorVisitSaveRequest
{
    public int DoctorId { get; set; }
    public DateTime VisitDateUtc { get; set; }
    public int? DurationMinutes { get; set; }
    public string? ProductsDiscussed { get; set; }
    public string? SamplesGiven { get; set; }
    public string? MaterialsLeft { get; set; }
    public string? FeedbackNotes { get; set; }
    public string? NextVisitRecommendation { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }

    /// <summary>Optional explicit link to the plan stop this visit fulfills (mobile "Today's Plan" flow) —
    /// when omitted, IsPlanned is inferred by matching doctor/representative/date against Approved plans.</summary>
    public int? VisitPlanItemId { get; set; }
}

public record DoctorFollowUpDto(
    int Id,
    int DoctorId,
    int? DoctorVisitId,
    FollowUpType Type,
    DateTime DueDateUtc,
    int OwnerRepresentativeId,
    string OwnerRepresentativeName,
    FollowUpStatus Status,
    string? OutcomeNotes);

public class DoctorFollowUpSaveRequest
{
    public int DoctorId { get; set; }
    public int? DoctorVisitId { get; set; }
    public FollowUpType Type { get; set; }
    public DateTime DueDateUtc { get; set; }
    public int OwnerRepresentativeId { get; set; }
    public string? OutcomeNotes { get; set; }
}

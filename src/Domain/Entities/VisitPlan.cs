using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// A representative's daily/weekly/monthly visit plan (spec 4.2) — planned doctor visits, submitted
/// for optional manager approval. Planned-vs-actual is derived by comparing <see cref="VisitPlanItem"/>
/// rows against the representative's logged <see cref="DoctorVisit"/> rows for the same period.
/// </summary>
public class VisitPlan : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public VisitPlanPeriodType PeriodType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public VisitPlanStatus Status { get; set; } = VisitPlanStatus.Draft;

    public DateTime? SubmittedAtUtc { get; set; }

    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public ICollection<VisitPlanItem> Items { get; set; } = new List<VisitPlanItem>();
}

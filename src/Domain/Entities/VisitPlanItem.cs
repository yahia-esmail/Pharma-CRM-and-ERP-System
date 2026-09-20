using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// One planned stop — a doctor visit or a pharmacy visit — within a <see cref="VisitPlan"/> (mobile
/// "Today's Plan" screen mixes both). Exactly one of <see cref="DoctorId"/>/<see cref="PharmacyId"/> is
/// set; the service layer enforces this, not a DB constraint. Planned-vs-actual (spec 4.2) is computed at
/// query time by matching against the representative's logged <see cref="DoctorVisit"/>/<see cref="PharmacyVisit"/>
/// rows for the same doctor-or-pharmacy/date — kept as a derived comparison rather than a stored link, so
/// logging a visit never needs to know about, or reach into, the planning module.
/// </summary>
public class VisitPlanItem : AuditableEntity
{
    public int VisitPlanId { get; set; }
    public VisitPlan VisitPlan { get; set; } = null!;

    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public int? PharmacyId { get; set; }
    public Pharmacy? Pharmacy { get; set; }

    public DateOnly PlannedDate { get; set; }
    public int Sequence { get; set; }
    public string? Notes { get; set; }
}

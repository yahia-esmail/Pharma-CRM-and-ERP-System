using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class DoctorFollowUp : AuditableEntity
{
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public int? DoctorVisitId { get; set; }
    public DoctorVisit? DoctorVisit { get; set; }

    public FollowUpType Type { get; set; }
    public DateTime DueDateUtc { get; set; }
    public int OwnerRepresentativeId { get; set; }
    public Representative OwnerRepresentative { get; set; } = null!;

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Open;
    public string? OutcomeNotes { get; set; }
}

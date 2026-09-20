using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Immutable record of a Doctor/Pharmacy being reassigned from one representative to another
/// (addendum 3.1). Never rewrites past transactions — those keep whichever RepresentativeId they were
/// created with; only future activity follows the new PrimaryRepresentativeId.</summary>
public class CustomerTransferLog : AuditableEntity
{
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int? PharmacyId { get; set; }
    public Pharmacy? Pharmacy { get; set; }

    public int FromRepresentativeId { get; set; }
    public Representative FromRepresentative { get; set; } = null!;
    public int ToRepresentativeId { get; set; }
    public Representative ToRepresentative { get; set; } = null!;

    public DateTime TransferDateUtc { get; set; }
    public string? Reason { get; set; }
    public string ApprovedByUserId { get; set; } = null!;
}

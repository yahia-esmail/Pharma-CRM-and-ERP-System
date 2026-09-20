using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>One customer in a Static CustomerList. Not used for Dynamic lists (their membership is
/// computed live from CustomerList's Filter* fields).</summary>
public class CustomerListItem : AuditableEntity
{
    public int CustomerListId { get; set; }
    public CustomerList CustomerList { get; set; } = null!;

    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int? PharmacyId { get; set; }
    public Pharmacy? Pharmacy { get; set; }

    public DateTime AddedAtUtc { get; set; }
    public string AddedByUserId { get; set; } = null!;
}

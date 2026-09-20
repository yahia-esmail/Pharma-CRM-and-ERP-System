using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// A representative handing collected funds over to Finance (spec 4.6). Requires finance-role
/// confirmation (enforced at the Application layer) before it reduces the representative's outstanding
/// financial custody balance — that balance is always Total Collected − Total Remitted, never edited directly.
/// </summary>
public class RemittanceTransaction : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateTime RemittanceDateUtc { get; set; }
    public PaymentMethod RemittanceMethod { get; set; }

    /// <summary>Identity user id of the finance user who confirmed receipt — a plain string, per spec 5.3's user-reference convention.</summary>
    public string ReceivingUserId { get; set; } = null!;

    public string? ReferenceNumber { get; set; }
}

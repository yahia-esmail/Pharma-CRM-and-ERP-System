using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// A payment a representative collected in the field, held in their financial custody until remitted
/// (spec 4.6). Whether it's still "Collected" or already "Remitted" is derived — never stored — by
/// FIFO-matching against the representative's <see cref="RemittanceTransaction"/> total (spec 5.3).
/// </summary>
public class Collection : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;

    /// <summary>The specific sale/invoice this payment settles — optional, supports partial/general payments (spec 4.6).</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public decimal Amount { get; set; }
    public DateTime CollectionDateUtc { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    /// <summary>How the amount is split across invoices; empty = applied to the oldest open invoices.</summary>
    public ICollection<CollectionAllocation> Allocations { get; set; } = new List<CollectionAllocation>();
}

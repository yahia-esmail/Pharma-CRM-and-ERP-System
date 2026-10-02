using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>The part of a <see cref="Collection"/> the rep applied to one invoice (wireframe 10 — one cheque
/// can settle several invoices). Whatever part of a collection isn't allocated is applied to the pharmacy's
/// oldest open invoices first (see PharmacyBalanceCalculator).</summary>
public class CollectionAllocation : BaseEntity
{
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public decimal Amount { get; set; }
}

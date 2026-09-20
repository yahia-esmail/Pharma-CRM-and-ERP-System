using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Created the moment an <see cref="Order"/> is confirmed (spec 4.3). Phase 3 records it against a
/// simplified stock check only — Phase 4 will add a StockMovementId reference column once the
/// warehouse/custody chain exists, completing the traceability chain from spec 4.11.
/// </summary>
public class Sale : AuditableEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime SaleDateUtc { get; set; }

    /// <summary>Snapshot of the confirmed order's total — a completed transaction record, not a live balance.</summary>
    public decimal TotalAmount { get; set; }
}

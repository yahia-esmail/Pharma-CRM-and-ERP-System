using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

public class OrderLine : AuditableEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Free/promotional units (addendum 3.4) — carried at zero value in the line total, but
    /// still physically leaves stock/custody alongside the paid Quantity on delivery.</summary>
    public int BonusQuantity { get; set; }

    /// <summary>Snapshot of the product's unit price at order time — never re-derived after the fact.</summary>
    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }
}

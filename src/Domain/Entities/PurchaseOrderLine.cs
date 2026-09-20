using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Ordered quantity is stored; received-to-date is always computed from PurchaseReceiptLine rows (spec 5.3).</summary>
public class PurchaseOrderLine : AuditableEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

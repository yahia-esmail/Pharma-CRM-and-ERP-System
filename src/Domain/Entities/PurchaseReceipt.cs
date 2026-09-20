using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Goods receipt against a purchase order (spec 4.7) — the origin point of the traceability chain
/// (spec 4.11). Each line creates a linked warehouse StockMovement(GoodsReceipt) via the Inventory
/// module, so a batch's full history threads back to the PO and supplier that brought it in.
/// </summary>
public class PurchaseReceipt : AuditableEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public DateTime ReceiptDateUtc { get; set; }

    public ICollection<PurchaseReceiptLine> Lines { get; set; } = new List<PurchaseReceiptLine>();
}

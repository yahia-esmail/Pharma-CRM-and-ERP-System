using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

public class PurchaseReceiptLine : AuditableEntity
{
    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;

    public int PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public int QuantityReceived { get; set; }

    /// <summary>The warehouse-side movement this receipt line created — the link back into the Inventory ledger.</summary>
    public int StockMovementId { get; set; }
    public StockMovement StockMovement { get; set; } = null!;
}

using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class PurchaseOrder : AuditableEntity
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public DateTime OrderDateUtc { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// One physical stock movement (spec 4.4). Which of SourceWarehouseId / DestinationWarehouseId /
/// RepresentativeId is populated, and which "direction" it plays, depends on <see cref="MovementType"/>:
/// GoodsReceipt (dest warehouse only), Transfer (source + dest warehouse), IssueToCustody (source
/// warehouse + dest representative), ReturnFromCustody (source representative + dest warehouse),
/// AdjustmentIncrease (dest warehouse only), AdjustmentDecrease (source warehouse only).
/// </summary>
public class StockMovement : AuditableEntity
{
    public StockMovementType MovementType { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public int Quantity { get; set; }

    public int? SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }

    public int? DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    /// <summary>The representative on the other end of an IssueToCustody/ReturnFromCustody movement.</summary>
    public int? RepresentativeId { get; set; }
    public Representative? Representative { get; set; }

    public DateTime MovementDateUtc { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReferenceNote { get; set; }
}

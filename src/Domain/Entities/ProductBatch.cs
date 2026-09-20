using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Batch/lot identity for a product (spec 4.4). Deliberately carries no quantity columns — a batch can
/// exist across several warehouses and representatives at once, so "how much of this batch is where" is
/// always computed from <see cref="StockMovement"/>/<see cref="CustodyTransaction"/> rows (spec 5.3),
/// never stored here.
/// </summary>
public class ProductBatch : AuditableEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string BatchNumber { get; set; } = null!;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
}

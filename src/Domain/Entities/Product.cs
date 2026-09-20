using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Product master data (spec 4.4). Introduced in Phase 3 because Order lines need it, with the full
/// field set from the spec's Inventory module so Phase 4 (batches, warehouses, stock movements) layers
/// on top of this entity rather than reshaping it.
/// </summary>
public class Product : AuditableEntity
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Category { get; set; }
    public string UnitOfMeasure { get; set; } = null!;

    public bool IsBatchTracked { get; set; }
    public bool IsExpiryTracked { get; set; }

    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }
}

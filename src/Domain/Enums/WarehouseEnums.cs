namespace PharmaERP.Domain.Enums;

public enum WarehouseType
{
    Main = 0,
    Regional = 1
}

/// <summary>
/// Every physical stock movement in the system (spec 4.4). Direction is implied by which of
/// SourceWarehouseId/DestinationWarehouseId/RepresentativeId is populated on the movement row —
/// see <see cref="Entities.StockMovement"/> for the field-by-field rules per type.
/// </summary>
public enum StockMovementType
{
    /// <summary>Stock-in from a supplier. Simplified/manual until Phase 5 adds real Purchase Orders.</summary>
    GoodsReceipt = 0,
    Transfer = 1,
    IssueToCustody = 2,
    ReturnFromCustody = 3,
    AdjustmentIncrease = 4,
    AdjustmentDecrease = 5
}

using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Warehouses;

public record WarehouseListItemDto(int Id, string Name, string? Location, WarehouseType Type);

public class WarehouseSaveRequest
{
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public WarehouseType Type { get; set; } = WarehouseType.Main;
    public string? ResponsibleUserId { get; set; }
}

public record ProductBatchDto(int Id, int ProductId, string ProductName, string BatchNumber,
    DateOnly? ManufactureDate, DateOnly ExpiryDate);

public class ProductBatchSaveRequest
{
    public int ProductId { get; set; }
    public string BatchNumber { get; set; } = null!;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
}

public record StockLevelDto(
    int WarehouseId, string WarehouseName, int ProductId, string ProductName,
    int? ProductBatchId, string? BatchNumber, DateOnly? ExpiryDate, int Quantity);

public record StockMovementDto(
    int Id, StockMovementType MovementType, int ProductId, string ProductName,
    string? BatchNumber, int Quantity, string? SourceWarehouseName, string? DestinationWarehouseName,
    string? RepresentativeName, DateTime MovementDateUtc, string? ReasonCode, string? ReferenceNote);

public class GoodsReceiptRequest
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public string? ReferenceNote { get; set; }
}

public class StockTransferRequest
{
    public int SourceWarehouseId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
    public string? ReferenceNote { get; set; }
}

public class StockAdjustmentRequest
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
    public bool IsIncrease { get; set; }
    public string ReasonCode { get; set; } = null!;
}

public record NearExpiryBatchDto(int ProductBatchId, int ProductId, string ProductName, string BatchNumber,
    DateOnly ExpiryDate, int DaysUntilExpiry, int WarehouseQuantity, int CustodyQuantity);

public record LowStockProductDto(int ProductId, string ProductName, string Sku, int ReorderLevel, int TotalWarehouseQuantity);

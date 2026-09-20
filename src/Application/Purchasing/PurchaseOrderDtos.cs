using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Purchasing;

public record PurchaseOrderListItemDto(
    int Id, int SupplierId, string SupplierName, DateTime OrderDateUtc, DateOnly? ExpectedDeliveryDate,
    PurchaseOrderStatus Status, decimal TotalAmount);

public record PurchaseOrderLineDto(
    int Id, int ProductId, string ProductName, int Quantity, decimal UnitCost, decimal LineTotal, int QuantityReceived);

public record PurchaseOrderDetailDto(
    int Id, int SupplierId, string SupplierName, DateTime OrderDateUtc, DateOnly? ExpectedDeliveryDate,
    PurchaseOrderStatus Status, IReadOnlyList<PurchaseOrderLineDto> Lines, decimal TotalAmount);

public class PurchaseOrderCreateRequest
{
    public int SupplierId { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
}

public class PurchaseOrderLineSaveRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseReceiptLineRequest
{
    public int PurchaseOrderLineId { get; set; }
    public int Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class PurchaseReceiptRequest
{
    public int PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; }
    public List<PurchaseReceiptLineRequest> Lines { get; set; } = [];
}

public record PurchaseReceiptDto(int Id, int PurchaseOrderId, int WarehouseId, string WarehouseName, DateTime ReceiptDateUtc);

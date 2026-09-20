using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Custody;

public record RepStockCustodyBalanceDto(
    int RepresentativeId,
    int ProductId,
    string ProductName,
    int? ProductBatchId,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    int Received,
    int Sold,
    int Returned,
    int Balance);

public record CustodyTransactionDto(
    int Id,
    CustodyTransactionType TransactionType,
    int ProductId,
    string ProductName,
    string? BatchNumber,
    int Quantity,
    DateTime TransactionDateUtc,
    string? ReasonCode,
    int? SaleId,
    int? SourceStockMovementId);

public class IssueToCustodyRequest
{
    public int WarehouseId { get; set; }
    public int RepresentativeId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
    public string? ReferenceNote { get; set; }
}

public class ReturnFromCustodyRequest
{
    public int RepresentativeId { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
    public string ReasonCode { get; set; } = null!;
}

public class CustodyTransferRequest
{
    public int FromRepresentativeId { get; set; }
    public int ToRepresentativeId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
}

public class StockReconciliationRequest
{
    public int RepresentativeId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int CountedBalance { get; set; }
    public string? Notes { get; set; }
}

public record StockReconciliationResultDto(int SystemBalance, int CountedBalance, int Variance, bool AdjustmentCreated);

/// <summary>Product-level (batch-agnostic) view of a representative's custody, addendum 3.7 — Reserved is
/// the portion earmarked by Approved-but-not-yet-Delivered orders; Available = Balance - Reserved is what
/// a new order can still draw against.</summary>
public record RepStockReservationDto(int ProductId, string ProductName, int Balance, int Reserved, int Available);

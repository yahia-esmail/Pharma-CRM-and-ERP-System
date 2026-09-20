using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Traceability;

public record TraceOriginDto(int SupplierId, string SupplierName, int PurchaseOrderId, int PurchaseReceiptId,
    DateTime ReceiptDateUtc, decimal UnitCost);

public record TraceMovementDto(int Id, StockMovementType MovementType, int Quantity, string? SourceLocation,
    string? DestinationLocation, DateTime MovementDateUtc, string? ReasonCode, string? ReferenceNote);

public record TraceCustodyEntryDto(int Id, int RepresentativeId, string RepresentativeName,
    CustodyTransactionType TransactionType, int Quantity, DateTime TransactionDateUtc, int? SaleId, string? ReasonCode);

public record TraceSaleSummaryDto(int SaleId, int OrderId, int PharmacyId, string PharmacyName,
    int RepresentativeId, string RepresentativeName, decimal Amount, DateTime SaleDateUtc,
    IReadOnlyList<TraceCollectionDto> Collections);

public record TraceCollectionDto(int CollectionId, decimal Amount, DateTime CollectionDateUtc, PaymentMethod PaymentMethod);

/// <summary>Full chain for one product batch (spec 4.11): Supplier PO → Goods Receipt → Warehouse → Custody → Sale → Collection.</summary>
public record BatchTraceDto(
    int ProductBatchId,
    int ProductId,
    string ProductName,
    string BatchNumber,
    DateOnly ExpiryDate,
    IReadOnlyList<TraceOriginDto> Origins,
    IReadOnlyList<TraceMovementDto> Movements,
    IReadOnlyList<TraceCustodyEntryDto> CustodyEntries,
    IReadOnlyList<TraceSaleSummaryDto> Sales);

/// <summary>Full chain anchored on one sale — same chain, walked backward from the transaction a manager is actually looking at.</summary>
public record SaleTraceDto(
    int SaleId,
    int OrderId,
    int PharmacyId,
    string PharmacyName,
    int RepresentativeId,
    string RepresentativeName,
    decimal Amount,
    DateTime SaleDateUtc,
    IReadOnlyList<TraceCollectionDto> Collections,
    decimal RepresentativeOutstandingCustody,
    IReadOnlyList<BatchTraceDto> ConsumedBatches);

using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Custody;

public interface ICustodyService
{
    Task<IReadOnlyList<RepStockCustodyBalanceDto>> GetBalancesAsync(int representativeId, CancellationToken ct = default);

    Task<IReadOnlyList<CustodyTransactionDto>> GetLedgerAsync(int representativeId, int? productId,
        CancellationToken ct = default);

    /// <summary>Warehouse → representative (spec 4.5) — paired with the warehouse-side StockMovement, never entered independently.</summary>
    Task<int> IssueAsync(IssueToCustodyRequest request, CancellationToken ct = default);

    Task<int> ReturnAsync(ReturnFromCustodyRequest request, CancellationToken ct = default);

    /// <summary>Representative → representative handover (spec 4.5), logged as a paired TransferOut/TransferIn.</summary>
    Task TransferAsync(CustodyTransferRequest request, CancellationToken ct = default);

    Task<StockReconciliationResultDto> ReconcileAsync(StockReconciliationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Stages (does not save) the CustodyTransaction(Sold) rows for a confirmed order's lines, FEFO across
    /// batches. Called by OrderService within the same unit of work as Order/Sale creation (spec 4.5:
    /// "cannot record a sale exceeding custody balance"). Takes the not-yet-saved Sale entity itself (not
    /// its id) so EF Core resolves the FK via the navigation once the whole unit of work is saved together.
    /// Throws ValidationFailedException if insufficient.
    /// </summary>
    Task DeductForSaleAsync(int representativeId, Sale sale, IReadOnlyList<(int ProductId, int Quantity)> lines,
        CancellationToken ct = default);

    /// <summary>A representative's custody balance for one product, summed across every batch.</summary>
    Task<int> GetProductBalanceAsync(int representativeId, int productId, CancellationToken ct = default);

    Task<int> GetBalanceAsync(int representativeId, int productId, int? productBatchId, CancellationToken ct = default);

    /// <summary>Reserved 3.7 — Balance minus whatever's already earmarked by this representative's other
    /// Approved-but-undelivered orders for this product. What OrderService.ApproveAsync checks before
    /// letting one more order reserve the same stock.</summary>
    Task<int> GetAvailableToSellAsync(int representativeId, int productId, CancellationToken ct = default);

    Task<IReadOnlyList<RepStockReservationDto>> GetReservationsAsync(int representativeId, CancellationToken ct = default);

    /// <summary>Pharmacy → representative handover (addendum 3.8's Customer→Representative return leg) —
    /// increases custody balance. Paired write only, called from the Returns approval workflow.</summary>
    Task ReceiveCustomerReturnAsync(int representativeId, int productId, int? productBatchId, int quantity,
        string? reasonCode, CancellationToken ct = default);
}

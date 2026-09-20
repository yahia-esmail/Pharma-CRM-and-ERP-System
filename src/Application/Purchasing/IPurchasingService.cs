using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Purchasing;

public interface IPurchasingService
{
    Task<PagedResult<PurchaseOrderListItemDto>> GetListAsync(PagedRequest request, int? supplierId,
        PurchaseOrderStatus? status, CancellationToken ct = default);

    Task<PurchaseOrderDetailDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateDraftAsync(PurchaseOrderCreateRequest request, CancellationToken ct = default);
    Task<int> AddLineAsync(int purchaseOrderId, PurchaseOrderLineSaveRequest request, CancellationToken ct = default);
    Task RemoveLineAsync(int purchaseOrderId, int lineId, CancellationToken ct = default);
    Task SendAsync(int purchaseOrderId, CancellationToken ct = default);
    Task CancelAsync(int purchaseOrderId, CancellationToken ct = default);

    /// <summary>
    /// Goods receipt against a PO (spec 4.7) — each line creates a linked warehouse StockMovement via
    /// the Inventory module (spec 4.4), completing the traceability chain (spec 4.11). Cannot exceed
    /// the ordered quantity on its line without explicit override (blocked here, per the same
    /// block-by-default pattern as the Phase 3 credit limit and Phase 4 custody checks).
    /// </summary>
    Task<int> ReceiveAsync(PurchaseReceiptRequest request, CancellationToken ct = default);
}

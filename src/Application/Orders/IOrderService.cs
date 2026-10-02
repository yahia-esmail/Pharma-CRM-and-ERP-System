using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Orders;

public interface IOrderService
{
    Task<PagedResult<OrderListItemDto>> GetListAsync(PagedRequest request, int? pharmacyId, int? representativeId,
        OrderStatus? status, CancellationToken ct = default);

    Task<OrderDetailDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateDraftAsync(int representativeId, OrderCreateRequest request, CancellationToken ct = default);

    /// <summary>Creates (<paramref name="orderId"/> null) or replaces a draft with all its lines, optionally
    /// submitting it, in one transaction. Returns the order id.</summary>
    Task<int> SaveDraftAsync(int representativeId, int? orderId, OrderSaveRequest request, CancellationToken ct = default);

    /// <summary>Edits order-level fields (currently just the destination pharmacy) — Draft only.</summary>
    Task UpdateAsync(int orderId, OrderUpdateRequest request, CancellationToken ct = default);

    Task<int> AddLineAsync(int orderId, OrderLineSaveRequest request, CancellationToken ct = default);

    /// <summary>Edits an existing line's quantity/bonus/discount — Draft only.</summary>
    Task UpdateLineAsync(int orderId, int lineId, OrderLineSaveRequest request, CancellationToken ct = default);

    Task RemoveLineAsync(int orderId, int lineId, CancellationToken ct = default);

    /// <summary>Draft -> Submitted, ready for manager approval (addendum 3.4).</summary>
    Task SubmitAsync(int orderId, CancellationToken ct = default);

    /// <summary>Submitted -> Approved. Enforces the pharmacy's credit limit here — before delivery, not after.</summary>
    Task ApproveAsync(int orderId, CancellationToken ct = default);

    /// <summary>Submitted -> Rejected, with a reason recorded for the representative.</summary>
    Task RejectAsync(int orderId, string reason, CancellationToken ct = default);

    /// <summary>Approved -> Delivered, snapshotting the order into a Sale (spec 4.3) and deducting the
    /// representative's stock custody (Quantity + BonusQuantity per line — bonus units leave stock too).</summary>
    Task<int> DeliverAsync(int orderId, CancellationToken ct = default);

    /// <summary>Allowed from Draft or Submitted — once Approved, an order is committed to delivery.</summary>
    Task CancelAsync(int orderId, CancellationToken ct = default);
}

using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Returns;

public interface IReturnService
{
    Task<IReadOnlyList<ReturnTransactionDto>> GetListAsync(int? representativeId, ReturnStatus? status,
        CancellationToken ct = default);

    Task<int> RequestAsync(int representativeId, string requestedByUserId, ReturnRequestSaveRequest request,
        CancellationToken ct = default);

    /// <summary>Applies the return's effect — increases the representative's custody (Customer leg) or
    /// moves stock from custody back to the warehouse (Representative leg) — by delegating to
    /// ICustodyService, the single place that writes CustodyTransaction/StockMovement rows.</summary>
    Task ApproveAsync(int returnId, string approvedByUserId, CancellationToken ct = default);

    Task RejectAsync(int returnId, string reason, CancellationToken ct = default);
}

using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierListItemDto>> GetListAsync(PagedRequest request, CancellationToken ct = default);
    Task<SupplierDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(SupplierSaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, SupplierSaveRequest request, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);

    Task<SupplierLedgerDto> GetLedgerAsync(int id, CancellationToken ct = default);

    Task<PagedResult<SupplierPaymentDto>> GetPaymentsAsync(PagedRequest request, int? supplierId, CancellationToken ct = default);
    Task<int> RecordPaymentAsync(SupplierPaymentSaveRequest request, CancellationToken ct = default);
}

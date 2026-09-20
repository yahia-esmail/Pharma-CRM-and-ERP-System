using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Sales;

public interface ISalesService
{
    Task<PagedResult<SaleListItemDto>> GetListAsync(PagedRequest request, int? pharmacyId, int? representativeId,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);

    Task<decimal> GetTotalSalesAsync(int? territoryId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Sales;

public class SalesService(IAppDbContext db) : ISalesService
{
    public async Task<PagedResult<SaleListItemDto>> GetListAsync(PagedRequest request, int? pharmacyId,
        int? representativeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var query = db.Sales.AsNoTracking().Where(s => !s.IsDeleted);

        if (pharmacyId.HasValue) query = query.Where(s => s.PharmacyId == pharmacyId);
        if (representativeId.HasValue) query = query.Where(s => s.RepresentativeId == representativeId);
        if (fromDate.HasValue) query = query.Where(s => s.SaleDateUtc >= fromDate.Value.ToDateTime(TimeOnly.MinValue));
        if (toDate.HasValue) query = query.Where(s => s.SaleDateUtc <= toDate.Value.ToDateTime(TimeOnly.MaxValue));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.SaleDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SaleListItemDto(s.Id, s.OrderId, s.PharmacyId, s.Pharmacy.Name, s.RepresentativeId,
                s.Representative.FullName, s.SaleDateUtc, s.TotalAmount))
            .ToListAsync(ct);

        return new PagedResult<SaleListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<decimal> GetTotalSalesAsync(int? territoryId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var query = db.Sales.AsNoTracking()
            .Where(s => !s.IsDeleted
                        && s.SaleDateUtc >= fromDate.ToDateTime(TimeOnly.MinValue)
                        && s.SaleDateUtc <= toDate.ToDateTime(TimeOnly.MaxValue));

        if (territoryId.HasValue)
            query = query.Where(s => s.Representative.TerritoryId == territoryId);

        return await query.SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
    }
}

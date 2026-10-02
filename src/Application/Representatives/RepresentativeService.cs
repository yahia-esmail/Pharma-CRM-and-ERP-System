using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Representatives;

public class RepresentativeService(IAppDbContext db) : IRepresentativeService
{
    public async Task<PagedResult<RepresentativeListItemDto>> GetListAsync(PagedRequest request, int? territoryId,
        CancellationToken ct = default)
    {
        var query = db.Representatives.AsNoTracking().Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(r => r.FullName.Contains(request.Search) || r.EmployeeCode.Contains(request.Search));
        if (territoryId.HasValue)
            query = query.Where(r => r.TerritoryId == territoryId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(r => r.FullName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RepresentativeListItemDto(
                r.Id, r.EmployeeCode, r.FullName, r.Level,
                r.Territory != null ? r.Territory.Name : null,
                r.ReportingManager != null ? r.ReportingManager.FullName : null,
                r.EmploymentStatus))
            .ToListAsync(ct);

        return new PagedResult<RepresentativeListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<RepresentativeDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var rep = await db.Representatives.AsNoTracking()
            .Include(r => r.Territory)
            .Include(r => r.ReportingManager)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), id);

        return new RepresentativeDetailDto(rep.Id, rep.EmployeeCode, rep.FullName, rep.Level, rep.EmploymentStatus,
            rep.TerritoryId, rep.Territory?.Name, rep.ReportingManagerId, rep.ReportingManager?.FullName,
            rep.Phone, rep.Email, rep.ApplicationUserId);
    }

    public async Task<int> CreateAsync(RepresentativeSaveRequest request, CancellationToken ct = default)
    {
        var rep = new Representative();
        Apply(rep, request);
        db.Representatives.Add(rep);
        await db.SaveChangesAsync(ct);
        return rep.Id;
    }

    public async Task UpdateAsync(int id, RepresentativeSaveRequest request, CancellationToken ct = default)
    {
        var rep = await db.Representatives.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), id);

        Apply(rep, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkApplicationUserAsync(int id, string applicationUserId, CancellationToken ct = default)
    {
        var rep = await db.Representatives.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), id);

        rep.ApplicationUserId = applicationUserId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReassignTerritoryAsync(int id, int? newTerritoryId, CancellationToken ct = default)
    {
        var rep = await db.Representatives.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), id);

        rep.TerritoryId = newTerritoryId;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var rep = await db.Representatives.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), id);

        rep.EmploymentStatus = EmploymentStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Representative rep, RepresentativeSaveRequest request)
    {
        rep.EmployeeCode = request.EmployeeCode;
        rep.FullName = request.FullName;
        rep.Level = request.Level;
        rep.EmploymentStatus = request.EmploymentStatus;
        rep.TerritoryId = request.TerritoryId;
        rep.ReportingManagerId = request.ReportingManagerId;
        rep.Phone = request.Phone;
        rep.Email = request.Email;
    }
}

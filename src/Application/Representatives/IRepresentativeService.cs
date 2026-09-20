using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Representatives;

public interface IRepresentativeService
{
    Task<PagedResult<RepresentativeListItemDto>> GetListAsync(PagedRequest request, int? territoryId,
        CancellationToken ct = default);
    Task<RepresentativeDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(RepresentativeSaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, RepresentativeSaveRequest request, CancellationToken ct = default);
    Task ReassignTerritoryAsync(int id, int? newTerritoryId, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
}

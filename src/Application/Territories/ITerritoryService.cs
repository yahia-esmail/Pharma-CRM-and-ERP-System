namespace PharmaERP.Application.Territories;

public interface ITerritoryService
{
    Task<IReadOnlyList<TerritoryListItemDto>> GetListAsync(CancellationToken ct = default);
    Task<TerritoryDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(TerritorySaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, TerritorySaveRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Territory hierarchy addendum 3.2: resolves a territory plus every territory beneath it
    /// (its full subtree), for scoping a manager's visibility down to their assigned level and below.</summary>
    Task<IReadOnlyList<int>> GetDescendantTerritoryIdsAsync(int territoryId, CancellationToken ct = default);
}

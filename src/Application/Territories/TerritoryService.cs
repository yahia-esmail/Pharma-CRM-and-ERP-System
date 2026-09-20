using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Territories;

public class TerritoryService(IAppDbContext db) : ITerritoryService
{
    public async Task<IReadOnlyList<TerritoryListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        var items = await db.Territories.AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Type).ThenBy(t => t.Name)
            .Select(t => new TerritoryListItemDto(
                t.Id, t.Name, t.Region, t.Type, t.ParentTerritoryId,
                t.ParentTerritory != null ? t.ParentTerritory.Name : null,
                t.DistrictManager != null ? t.DistrictManager.FullName : null,
                t.Representatives.Count(r => !r.IsDeleted)))
            .ToListAsync(ct);
        return items;
    }

    public async Task<TerritoryDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var territory = await db.Territories.AsNoTracking()
            .Include(t => t.DistrictManager)
            .Include(t => t.ParentTerritory)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Territory), id);

        return new TerritoryDetailDto(territory.Id, territory.Name, territory.Region, territory.Description,
            territory.Type, territory.ParentTerritoryId, territory.ParentTerritory?.Name,
            territory.DistrictManagerId, territory.DistrictManager?.FullName);
    }

    public async Task<int> CreateAsync(TerritorySaveRequest request, CancellationToken ct = default)
    {
        await ValidateParentAsync(null, request, ct);

        var territory = new Territory();
        Apply(territory, request);
        db.Territories.Add(territory);
        await db.SaveChangesAsync(ct);
        return territory.Id;
    }

    public async Task UpdateAsync(int id, TerritorySaveRequest request, CancellationToken ct = default)
    {
        var territory = await db.Territories.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Territory), id);

        await ValidateParentAsync(id, request, ct);

        Apply(territory, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var territory = await db.Territories.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Territory), id);

        var hasReps = await db.Representatives.AnyAsync(r => r.TerritoryId == id && !r.IsDeleted, ct);
        if (hasReps)
            throw new ValidationFailedException("Cannot delete a territory that still has assigned representatives.");

        var hasChildren = await db.Territories.AnyAsync(t => t.ParentTerritoryId == id && !t.IsDeleted, ct);
        if (hasChildren)
            throw new ValidationFailedException("Cannot delete a territory that still has child territories beneath it.");

        territory.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetDescendantTerritoryIdsAsync(int territoryId, CancellationToken ct = default)
    {
        var all = await db.Territories.AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new { t.Id, t.ParentTerritoryId })
            .ToListAsync(ct);

        var byParent = all.Where(t => t.ParentTerritoryId.HasValue)
            .GroupBy(t => t.ParentTerritoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());

        var result = new List<int> { territoryId };
        var queue = new Queue<int>();
        queue.Enqueue(territoryId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byParent.TryGetValue(current, out var children)) continue;
            foreach (var childId in children)
            {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }
        return result;
    }

    /// <summary>A parent must be one level above its child in the hierarchy (Governorate > City > District >
    /// Area) and cannot be the territory's own descendant (would create a cycle).</summary>
    private async Task ValidateParentAsync(int? territoryId, TerritorySaveRequest request, CancellationToken ct)
    {
        if (request.ParentTerritoryId is not { } parentId) return;

        var parent = await db.Territories.AsNoTracking().FirstOrDefaultAsync(t => t.Id == parentId && !t.IsDeleted, ct)
            ?? throw new ValidationFailedException("The selected parent territory does not exist.");

        if (parent.Type >= request.Type)
            throw new ValidationFailedException(
                $"A {request.Type} cannot be placed under a {parent.Type} — parents must be a broader hierarchy level.");

        if (territoryId.HasValue)
        {
            var descendants = await GetDescendantTerritoryIdsAsync(territoryId.Value, ct);
            if (descendants.Contains(parentId))
                throw new ValidationFailedException("Cannot set a territory's own descendant as its parent.");
        }
    }

    private static void Apply(Territory territory, TerritorySaveRequest request)
    {
        territory.Name = request.Name;
        territory.Region = request.Region;
        territory.Description = request.Description;
        territory.Type = request.Type;
        territory.ParentTerritoryId = request.ParentTerritoryId;
        territory.DistrictManagerId = request.DistrictManagerId;
    }
}

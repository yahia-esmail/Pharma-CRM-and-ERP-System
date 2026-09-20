using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Territories;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Territory hierarchy lookup for the mobile app (addendum 3.2) — a picker for the Expense screen
/// and a basis for territory-based filtering. Read-only here; creating/editing territories stays an
/// admin-only Web.Mvc action, not a mobile capability.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TerritoriesController(ITerritoryService territoryService) : ControllerBase
{
    /// <summary>All territories when <paramref name="parentId"/> is omitted, or just the next hierarchy
    /// level (its direct children) when given — the client builds the tree from the flat, parent-linked list.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TerritoryListItemDto>>> GetList(
        [FromQuery] int? parentId, CancellationToken ct)
    {
        var territories = await territoryService.GetListAsync(ct);

        if (parentId.HasValue)
            territories = territories.Where(t => t.ParentTerritoryId == parentId).ToList();

        return Ok(territories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TerritoryDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await territoryService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

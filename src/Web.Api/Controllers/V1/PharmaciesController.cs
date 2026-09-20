using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Pharmacy directory + visit check-in surface consumed by the mobile app (spec 4.10).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.PharmaciesView)]
public class PharmaciesController(IPharmacyService pharmacyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PharmacyListItemDto>>> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await pharmacyService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, Search = search }, null, null, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PharmacyDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await pharmacyService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:int}/visits")]
    public async Task<ActionResult<IReadOnlyList<PharmacyVisitDto>>> GetVisits(int id, CancellationToken ct)
        => Ok(await pharmacyService.GetVisitsAsync(id, ct));

    [HttpPost("{id:int}/visits")]
    [Authorize(Policy = Policies.PharmaciesEdit)]
    public async Task<ActionResult> AddVisit(int id, PharmacyVisitSaveRequest request, CancellationToken ct)
    {
        var repIdClaim = User.FindFirst("RepresentativeId")?.Value;
        if (!int.TryParse(repIdClaim, out var repId))
            return Forbid();

        request.PharmacyId = id;
        var visitId = await pharmacyService.AddVisitAsync(repId, request, ct);
        return CreatedAtAction(nameof(GetVisits), new { id }, new { id = visitId });
    }

    [HttpGet("{id:int}/ledger")]
    public async Task<ActionResult<PharmacyLedgerDto>> GetLedger(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await pharmacyService.GetLedgerAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

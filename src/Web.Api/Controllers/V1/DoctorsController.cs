using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Doctors;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>
/// Read/write surface for the Doctors module consumed by the mobile app (spec 4.10). Full plan/visit
/// endpoints for offline sync land in Phase 2; this is the Phase 1 groundwork so the API versioning
/// and auth pipeline are proven end-to-end.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.DoctorsView)]
public class DoctorsController(IDoctorService doctorService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<DoctorListItemDto>>> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await doctorService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, Search = search },
            null, null, null, null, null, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DoctorDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await doctorService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:int}/visits")]
    public async Task<ActionResult<IReadOnlyList<DoctorVisitDto>>> GetVisits(int id, CancellationToken ct)
        => Ok(await doctorService.GetVisitsAsync(id, ct));

    [HttpPost("{id:int}/visits")]
    [Authorize(Policy = Policies.DoctorsEdit)]
    public async Task<ActionResult> AddVisit(int id, DoctorVisitSaveRequest request, CancellationToken ct)
    {
        var repIdClaim = User.FindFirst("RepresentativeId")?.Value;
        if (!int.TryParse(repIdClaim, out var repId))
            return Forbid();

        request.DoctorId = id;
        var visitId = await doctorService.AddVisitAsync(repId, request, ct);
        return CreatedAtAction(nameof(GetVisits), new { id }, new { id = visitId });
    }
}

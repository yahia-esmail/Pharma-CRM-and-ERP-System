using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>"My Financial Custody" + collection entry for the mobile app (spec 4.10).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.CollectionsView)]
public class CollectionsController(ICollectionService collectionService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<CollectionDto>>> GetMine(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25,
        [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null, CancellationToken ct = default)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();
        return Ok(await collectionService.GetCollectionsAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, repId, null, dateFrom, dateTo, ct));
    }

    [HttpGet("mine/custody")]
    public async Task<ActionResult<RepFinancialCustodyDto>> GetMyCustody(CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();

        try
        {
            return Ok(await collectionService.GetFinancialCustodyAsync(repId, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.CollectionsCreate)]
    public async Task<ActionResult> Create(CollectionSaveRequest request, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();

        try
        {
            var id = await collectionService.RecordCollectionAsync(repId, request, ct);
            return CreatedAtAction(nameof(GetMine), new { id });
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    private int? CurrentRepresentativeId()
    {
        var claim = User.FindFirst("RepresentativeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

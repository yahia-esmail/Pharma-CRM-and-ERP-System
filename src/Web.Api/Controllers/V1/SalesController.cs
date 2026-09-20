using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Sales;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>A representative's own sales history for the mobile app (spec 4.3/4.10) — Sales are only ever
/// created by OrderService.DeliverAsync, so this module is read-only here.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.SalesView)]
public class SalesController(ISalesService salesService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<SaleListItemDto>>> GetMine(
        [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        if (currentUser.RepresentativeId is not { } repId) return Forbid();

        return Ok(await salesService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, null, repId, dateFrom, dateTo, ct));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Returns;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Formal Returns module for the mobile app (addendum 3.8) — both the Customer(Pharmacy)→
/// Representative and Representative→Warehouse flows, each requiring approval before it touches Stock
/// Custody / Warehouse Inventory. (Distinct from the simpler, immediate-effect Custody/mine/return
/// endpoint, which only ever covered the Representative→Warehouse leg.)</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.CustodyView)]
public class ReturnsController(IReturnService returnService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReturnTransactionDto>>> GetMine(
        [FromQuery] ReturnStatus? status = null, CancellationToken ct = default)
    {
        if (currentUser.RepresentativeId is not { } repId) return Forbid();

        return Ok(await returnService.GetListAsync(repId, status, ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CustodyReturn)]
    public async Task<ActionResult> RequestReturn(ReturnRequestSaveRequest request, CancellationToken ct)
    {
        if (currentUser.RepresentativeId is not { } repId) return Forbid();
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            var id = await returnService.RequestAsync(repId, userId, request, ct);
            return CreatedAtAction(nameof(GetMine), new { id }, new { id });
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<ActionResult> Approve(int id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await returnService.ApproveAsync(id, userId, ct);
            return NoContent();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<ActionResult> Reject(int id, [FromBody] string reason, CancellationToken ct)
    {
        try
        {
            await returnService.RejectAsync(id, reason, ct);
            return NoContent();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

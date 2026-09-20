using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Orders;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Order entry from the field (spec 4.10 mobile "Order entry" screen).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.OrdersView)]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] int? pharmacyId = null,
        CancellationToken ct = default)
    {
        var repId = CurrentRepresentativeId();
        return Ok(await orderService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, pharmacyId, repId, null, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await orderService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> Create(OrderCreateRequest request, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId)
            return Forbid();

        var id = await orderService.CreateDraftAsync(repId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPatch("{id:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> Update(int id, OrderUpdateRequest request, CancellationToken ct)
    {
        try
        {
            await orderService.UpdateAsync(id, request, ct);
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

    [HttpPost("{id:int}/lines")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> AddLine(int id, OrderLineSaveRequest request, CancellationToken ct)
    {
        try
        {
            var lineId = await orderService.AddLineAsync(id, request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id = lineId });
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

    [HttpPatch("{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> UpdateLine(int id, int lineId, OrderLineSaveRequest request, CancellationToken ct)
    {
        try
        {
            await orderService.UpdateLineAsync(id, lineId, request, ct);
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

    [HttpDelete("{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> RemoveLine(int id, int lineId, CancellationToken ct)
    {
        try
        {
            await orderService.RemoveLineAsync(id, lineId, ct);
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

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await orderService.CancelAsync(id, ct);
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

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> Submit(int id, CancellationToken ct)
    {
        try
        {
            await orderService.SubmitAsync(id, ct);
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

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.OrdersApprove)]
    public async Task<ActionResult> Approve(int id, CancellationToken ct)
    {
        try
        {
            await orderService.ApproveAsync(id, ct);
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
    [Authorize(Policy = Policies.OrdersApprove)]
    public async Task<ActionResult> Reject(int id, [FromBody] string reason, CancellationToken ct)
    {
        try
        {
            await orderService.RejectAsync(id, reason, ct);
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

    [HttpPost("{id:int}/deliver")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<ActionResult> Deliver(int id, CancellationToken ct)
    {
        try
        {
            var saleId = await orderService.DeliverAsync(id, ct);
            return Ok(new { saleId });
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

    private int? CurrentRepresentativeId()
    {
        var claim = User.FindFirst("RepresentativeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

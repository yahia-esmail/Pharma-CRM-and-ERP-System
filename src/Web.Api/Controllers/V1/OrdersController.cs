using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Orders;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Order entry from the field (spec 4.10 mobile "Order entry" screen). A representative only ever
/// sees and changes their own orders (enforced in OrderService).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.OrdersView)]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] int? pharmacyId = null,
        [FromQuery] OrderStatus? status = null, CancellationToken ct = default)
    {
        var repId = this.RepresentativeId();
        return Ok(await orderService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, pharmacyId, repId, status, ct));
    }

    [HttpGet("{id:int}")]
    public Task<ActionResult> GetById(int id, CancellationToken ct) =>
        Run(async () => Ok(await orderService.GetByIdAsync(id, ct)));

    /// <summary>Creates an order. With <c>lines</c> (and optionally <c>submit</c>) the whole order is saved in
    /// one transaction — what the field app's offline outbox sends.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OrdersEdit)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public Task<ActionResult> Create(OrderSaveRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            var id = await orderService.SaveDraftAsync(repId, null, request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        });

    /// <summary>Replaces a draft — pharmacy and every line — and optionally submits it, in one transaction.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult> Replace(int id, OrderSaveRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId => Ok(new { id = await orderService.SaveDraftAsync(repId, id, request, ct) }));

    [HttpPatch("{id:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> Update(int id, OrderUpdateRequest request, CancellationToken ct) =>
        Run(async () => { await orderService.UpdateAsync(id, request, ct); return NoContent(); });

    [HttpPost("{id:int}/lines")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> AddLine(int id, OrderLineSaveRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            var lineId = await orderService.AddLineAsync(id, request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id = lineId });
        });

    [HttpPatch("{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> UpdateLine(int id, int lineId, OrderLineSaveRequest request, CancellationToken ct) =>
        Run(async () => { await orderService.UpdateLineAsync(id, lineId, request, ct); return NoContent(); });

    [HttpDelete("{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> RemoveLine(int id, int lineId, CancellationToken ct) =>
        Run(async () => { await orderService.RemoveLineAsync(id, lineId, ct); return NoContent(); });

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> Cancel(int id, CancellationToken ct) =>
        Run(async () => { await orderService.CancelAsync(id, ct); return NoContent(); });

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> Submit(int id, CancellationToken ct) =>
        Run(async () => { await orderService.SubmitAsync(id, ct); return NoContent(); });

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.OrdersApprove)]
    public Task<ActionResult> Approve(int id, CancellationToken ct) =>
        Run(async () => { await orderService.ApproveAsync(id, ct); return NoContent(); });

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.OrdersApprove)]
    public Task<ActionResult> Reject(int id, [FromBody] string reason, CancellationToken ct) =>
        Run(async () => { await orderService.RejectAsync(id, reason, ct); return NoContent(); });

    [HttpPost("{id:int}/deliver")]
    [Authorize(Policy = Policies.OrdersEdit)]
    public Task<ActionResult> Deliver(int id, CancellationToken ct) =>
        Run(async () => Ok(new { saleId = await orderService.DeliverAsync(id, ct) }));

    private async Task<ActionResult> Run(Func<Task<ActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
    }
}

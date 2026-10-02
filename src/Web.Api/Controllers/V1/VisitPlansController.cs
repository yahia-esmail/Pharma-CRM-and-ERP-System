using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Visit plan surface consumed by the mobile app (spec 4.10) — Today's Plan is the primary mobile screen.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.VisitPlansView)]
public class VisitPlansController(IVisitPlanService visitPlanService, ICurrentUserService currentUser,
    IBusinessCalendar calendar) : ControllerBase
{
    /// <summary>The rep's stops for today — "today" in the business time zone (Business:TimeZone), not UTC.</summary>
    [HttpGet("today")]
    public async Task<ActionResult<IReadOnlyList<VisitPlanItemDto>>> GetTodaysPlan(CancellationToken ct)
    {
        var repId = CurrentRepresentativeId();
        if (repId is null) return Forbid();

        return Ok(await visitPlanService.GetTodaysPlanAsync(repId.Value, calendar.Today, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<VisitPlanListItemDto>>> GetMine(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var repId = CurrentRepresentativeId();
        if (repId is null) return Forbid();

        return Ok(await visitPlanService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, repId, null, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VisitPlanDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await visitPlanService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:int}/plan-vs-actual")]
    public async Task<ActionResult<PlanVsActualDto>> GetPlanVsActual(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await visitPlanService.GetPlanVsActualAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<ActionResult> Create(VisitPlanCreateRequest request, CancellationToken ct)
    {
        try
        {
            var id = await visitPlanService.CreateDraftAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpPost("{id:int}/items")]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<ActionResult> AddItem(int id, VisitPlanItemSaveRequest request, CancellationToken ct)
    {
        try
        {
            var itemId = await visitPlanService.AddItemAsync(id, request, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id = itemId });
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

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<ActionResult> RemoveItem(int id, int itemId, CancellationToken ct)
    {
        try
        {
            await visitPlanService.RemoveItemAsync(id, itemId, ct);
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
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<ActionResult> Submit(int id, CancellationToken ct)
    {
        try
        {
            await visitPlanService.SubmitAsync(id, ct);
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
    [Authorize(Policy = Policies.VisitPlansApprove)]
    public async Task<ActionResult> Approve(int id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await visitPlanService.ApproveAsync(id, userId, ct);
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
    [Authorize(Policy = Policies.VisitPlansApprove)]
    public async Task<ActionResult> Reject(int id, [FromBody] string reason, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await visitPlanService.RejectAsync(id, userId, reason, ct);
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

    private int? CurrentRepresentativeId()
    {
        var claim = User.FindFirst("RepresentativeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

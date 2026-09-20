using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Expenses;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Expense claim submission for the mobile app (addendum 3.9).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.ExpensesView)]
public class ExpensesController(IExpenseService expenseService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> GetMine(
        [FromQuery] ExpenseStatus? status = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        return Ok(await expenseService.GetMineAsync(userId, status,
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ExpensesCreate)]
    public async Task<ActionResult> Create(ExpenseSaveRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            var id = await expenseService.CreateAsync(userId, request, ct);
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

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Policies.ExpensesCreate)]
    public async Task<ActionResult> Submit(int id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await expenseService.SubmitAsync(id, userId, ct);
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
    [Authorize(Policy = Policies.ExpensesApprove)]
    public async Task<ActionResult> Approve(int id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await expenseService.ApproveAsync(id, userId, ct);
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
    [Authorize(Policy = Policies.ExpensesApprove)]
    public async Task<ActionResult> Reject(int id, [FromBody] string reason, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await expenseService.RejectAsync(id, userId, reason, ct);
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

    [HttpPost("{id:int}/reimburse")]
    [Authorize(Policy = Policies.ExpensesReimburse)]
    public async Task<ActionResult> Reimburse(int id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        try
        {
            await expenseService.ReimburseAsync(id, userId, ct);
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

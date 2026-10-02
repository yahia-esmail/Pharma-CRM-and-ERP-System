using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Visits;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Manager review of customer locations proposed from the field (plan 7.9).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.LocationsReview)]
public class LocationProposalsController(ILocationProposalService proposals) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationProposalDto>>> Get([FromQuery] LocationProposalStatus? status, CancellationToken ct)
        => Ok(await proposals.GetAsync(status ?? LocationProposalStatus.Pending, ct));

    [HttpPost("{id:int}/approve")]
    public Task<ActionResult> Approve(int id, LocationProposalReviewRequest request, CancellationToken ct) =>
        ReviewAsync(() => proposals.ApproveAsync(id, ReviewerId, request.Note, ct));

    [HttpPost("{id:int}/reject")]
    public Task<ActionResult> Reject(int id, LocationProposalReviewRequest request, CancellationToken ct) =>
        ReviewAsync(() => proposals.RejectAsync(id, ReviewerId, request.Note, ct));

    private string ReviewerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<ActionResult> ReviewAsync(Func<Task> review)
    {
        try
        {
            await review();
            return NoContent();
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ValidationFailedException ex) { return BadRequest(new ProblemDetails { Title = ex.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Doctors;
using PharmaERP.Application.Visits;
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
public class DoctorsController(IDoctorService doctorService, IVisitSessionService visitSessions,
    ILocationProposalService locationProposals) : ControllerBase
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

    // ---- Mobile check-in → check-out flow (plan 6.1 item 6) ---------------------------------------
    // Called through the field app's outbox: each request carries an Idempotency-Key and X-Client-Sent-At.

    /// <summary>Starts a visit at this doctor. Returns the visit id the check-out refers to, plus the
    /// server's provisional validation verdict.</summary>
    [HttpPost("{id:int}/visits/check-in")]
    [Authorize(Policy = Policies.DoctorsEdit)]
    [ProducesResponseType(typeof(VisitCheckInResult), StatusCodes.Status201Created)]
    public Task<ActionResult> CheckIn(int id, VisitCheckInRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            var result = await visitSessions.CheckInDoctorAsync(repId, id, request, Request.ClientSentAtUtc(), ct);
            return CreatedAtAction(nameof(GetVisits), new { id }, result);
        });

    /// <summary>Completes a visit. The duration is computed here from the check-in and check-out times.</summary>
    [HttpPost("{id:int}/visits/{visitId:int}/check-out")]
    [Authorize(Policy = Policies.DoctorsEdit)]
    [ProducesResponseType(typeof(VisitCheckOutResult), StatusCodes.Status200OK)]
    public Task<ActionResult> CheckOut(int id, int visitId, DoctorVisitCheckOutRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
            Ok(await visitSessions.CheckOutDoctorAsync(repId, id, visitId, request, Request.ClientSentAtUtc(), ct)));

    [HttpPost("{id:int}/visits/{visitId:int}/cancel")]
    [Authorize(Policy = Policies.DoctorsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult> CancelVisit(int id, int visitId, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            await visitSessions.CancelDoctorVisitAsync(repId, id, visitId, ct);
            return NoContent();
        });

    /// <summary>Proposes this doctor's location from an on-site GPS fix (plan 7.9). A first location with a
    /// good fix is applied immediately; otherwise it waits for a manager.</summary>
    [HttpPost("{id:int}/location-proposals")]
    [Authorize(Policy = Policies.DoctorsEdit)]
    [ProducesResponseType(typeof(LocationProposalResult), StatusCodes.Status200OK)]
    public Task<ActionResult> ProposeLocation(int id, LocationProposalRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
            Ok(await locationProposals.ProposeForDoctorAsync(repId, id, request, ct)));
}

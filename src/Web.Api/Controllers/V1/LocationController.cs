using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Location;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Periodic GPS pings from the mobile app during working hours (spec 4.10, with disclosure to the rep).</summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class LocationController(ILocationService locationService) : ControllerBase
{
    [HttpPost("ping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Ping(LocationPingRequest request, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId)
            return Forbid();

        try
        {
            await locationService.RecordPingAsync(repId, request, ct);
            return NoContent();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    /// <summary>Uploads points the app buffered (up to 500 per call). Safe to re-send: points whose
    /// clientId is already stored are counted as duplicates, not stored again.</summary>
    [HttpPost("pings")]
    [ProducesResponseType(typeof(LocationPingBatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationPingBatchResult>> Pings(LocationPingBatchRequest request, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId)
            return Forbid();

        try
        {
            return Ok(await locationService.RecordPingsAsync(repId, request.Points, ct));
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpGet("latest")]
    [Authorize(Policy = Policies.LocationView)]
    public async Task<ActionResult<IReadOnlyList<RepresentativeLocationDto>>> GetLatest(
        [FromQuery] int? territoryId, CancellationToken ct)
        => Ok(await locationService.GetLatestLocationsAsync(territoryId, ct));

    private int? CurrentRepresentativeId() =>
        int.TryParse(User.FindFirst("RepresentativeId")?.Value, out var repId) ? repId : null;
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Location;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Periodic GPS pings from the mobile app during working hours (spec 4.10, with disclosure to the rep).</summary>
[ApiController]
[Route("api/v1/[controller]")]
public class LocationController(ILocationService locationService) : ControllerBase
{
    [HttpPost("ping")]
    public async Task<ActionResult> Ping(LocationPingRequest request, CancellationToken ct)
    {
        var claim = User.FindFirst("RepresentativeId")?.Value;
        if (!int.TryParse(claim, out var repId))
            return Forbid();

        await locationService.RecordPingAsync(repId, request, ct);
        return NoContent();
    }

    [HttpGet("latest")]
    [Authorize(Policy = Policies.LocationView)]
    public async Task<ActionResult<IReadOnlyList<RepresentativeLocationDto>>> GetLatest(
        [FromQuery] int? territoryId, CancellationToken ct)
        => Ok(await locationService.GetLatestLocationsAsync(territoryId, ct));
}

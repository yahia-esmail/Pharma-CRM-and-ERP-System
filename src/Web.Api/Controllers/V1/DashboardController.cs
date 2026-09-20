using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Dashboard;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Home-screen summary for the mobile app (addendum 3.11) — a representative's own daily/monthly
/// figures. Distinct from the web Management Dashboard, which is territory-scoped and manager-only. Scoped
/// to the caller's own RepresentativeId claim, so no role policy beyond being signed in is needed.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController(IRepresentativeDashboardService dashboardService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<RepresentativeDashboardDto>> GetMine(CancellationToken ct)
    {
        if (currentUser.RepresentativeId is not { } repId) return Forbid();

        try
        {
            return Ok(await dashboardService.GetMineAsync(repId, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

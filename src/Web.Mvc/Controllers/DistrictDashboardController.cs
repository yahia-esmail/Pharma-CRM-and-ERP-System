using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.DistrictDashboard;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>District Manager Dashboard (addendum 3.11) — District -> Representative -> Customer ->
/// Transaction drill-down, distinct from the consolidated Management Dashboard.</summary>
[Authorize(Policy = Policies.DashboardView)]
public class DistrictDashboardController(IDistrictDashboardService districtDashboardService, IAppDbContext db,
    ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(int? territoryId, int? year, int? month)
    {
        // A District Manager is implicitly scoped to their own territory (and everything beneath it) —
        // no cross-territory picker for them, same enforcement as the Management Dashboard.
        var scopedTerritoryId = currentUser.HasUnrestrictedAccess ? territoryId : currentUser.TerritoryId;

        var vm = new DistrictDashboardViewModel { TerritoryId = scopedTerritoryId };

        if (currentUser.HasUnrestrictedAccess)
        {
            vm.Territories = await db.Territories.AsNoTracking().Where(t => !t.IsDeleted)
                .OrderBy(t => t.Type).ThenBy(t => t.Name)
                .Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToListAsync();
        }

        if (scopedTerritoryId is { } id)
        {
            var today = DateTime.Today;
            try
            {
                vm.Summary = await districtDashboardService.GetSummaryAsync(id, year ?? today.Year, month ?? today.Month);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        return View(vm);
    }

    public async Task<IActionResult> Representative(int id, int? year, int? month)
    {
        var today = DateTime.Today;
        try
        {
            var drilldown = await districtDashboardService.GetRepresentativeDrilldownAsync(id, year ?? today.Year, month ?? today.Month);
            return View(new RepresentativeDrilldownViewModel { Drilldown = drilldown });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

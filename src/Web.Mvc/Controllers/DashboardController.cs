using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Dashboard;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>Executive Summary / Management Dashboard (spec 4.8) — the single-page rollup across every module.</summary>
[Authorize(Policy = Policies.DashboardView)]
public class DashboardController(IDashboardService dashboardService, IAppDbContext db, ICurrentUserService currentUser)
    : Controller
{
    public async Task<IActionResult> Index(int? territoryId, int? year, int? month)
    {
        // A District Manager is implicitly scoped to their own territory — no cross-territory picker for them.
        var scopedTerritoryId = currentUser.HasUnrestrictedAccess ? territoryId : currentUser.TerritoryId;

        var today = DateTime.Today;
        var vm = new DashboardViewModel
        {
            Summary = await dashboardService.GetSummaryAsync(scopedTerritoryId, year ?? today.Year, month ?? today.Month),
            TerritoryId = scopedTerritoryId
        };

        if (currentUser.HasUnrestrictedAccess)
        {
            vm.Territories = await db.Territories.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Name)
                .Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToListAsync();
        }

        return View(vm);
    }
}

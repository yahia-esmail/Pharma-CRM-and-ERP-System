using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Performance;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Infrastructure;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.PerformanceView)]
public class PerformanceController(IPerformanceService performanceService) : Controller
{
    public async Task<IActionResult> Index(int? territoryId, int? year, int? month)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        var scorecards = await performanceService.GetScorecardsAsync(territoryId, y, m);

        return View(new PerformanceDashboardViewModel
        {
            Year = y, Month = m, TerritoryId = territoryId, Scorecards = scorecards
        });
    }

    public async Task<IActionResult> Export(int? territoryId, int? year, int? month)
    {
        var today = DateTime.Today;
        var scorecards = await performanceService.GetScorecardsAsync(territoryId, year ?? today.Year, month ?? today.Month);

        var csv = CsvExport.Build(
            ["Representative", "Territory", "Planned Visits", "Actual Visits", "Visit Coverage %", "Target %", "Doctor Coverage %", "Visit Quality %"],
            scorecards.Select(s => new object?[]
            {
                s.RepresentativeName, s.TerritoryName, s.PlannedVisits, s.ActualVisits,
                s.VisitCoveragePercent, s.VisitCoverageTarget, s.DoctorCoveragePercent, s.VisitQualityPercent
            }));

        return File(csv, "text/csv", $"performance-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PerformanceManageTargets)]
    public async Task<IActionResult> SetTarget(PerformanceTargetFormViewModel vm)
    {
        if (ModelState.IsValid)
        {
            await performanceService.SetTargetAsync(new PerformanceTargetSaveRequest
            {
                RepresentativeId = vm.RepresentativeId,
                Year = vm.Year,
                Month = vm.Month,
                MetricKey = PerformanceMetricKeys.VisitCoverage,
                TargetValue = vm.TargetValue
            });
            TempData["StatusMessage"] = "Target updated.";
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to set target — check the values entered.";
        }

        return RedirectToAction(nameof(Index), new { year = vm.Year, month = vm.Month });
    }
}

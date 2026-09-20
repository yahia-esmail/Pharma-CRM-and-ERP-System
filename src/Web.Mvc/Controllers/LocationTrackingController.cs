using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Location;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>Manager map view of representative visit locations (spec 4.2, 4.10).</summary>
[Authorize(Policy = Policies.LocationView)]
public class LocationTrackingController(ILocationService locationService) : Controller
{
    public async Task<IActionResult> Index(int? territoryId)
    {
        ViewBag.TerritoryId = territoryId;
        return View(await locationService.GetLatestLocationsAsync(territoryId));
    }

    public async Task<IActionResult> Trail(int representativeId, DateOnly? date)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);
        ViewBag.RepresentativeId = representativeId;
        ViewBag.Date = day;
        return View(await locationService.GetRepresentativeTrailAsync(representativeId, day));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Representatives;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.RepresentativesView)]
public class RepresentativesController(IRepresentativeService repService, IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await repService.GetListAsync(new PagedRequest { PageNumber = page, Search = search }, null);
        ViewBag.Search = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            return View(await repService.GetByIdAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new RepresentativeFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Create(RepresentativeFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        var id = await repService.CreateAsync(ToRequest(vm));
        TempData["StatusMessage"] = "Representative created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        RepresentativeDetailDto rep;
        try
        {
            rep = await repService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new RepresentativeFormViewModel
        {
            Id = rep.Id,
            EmployeeCode = rep.EmployeeCode,
            FullName = rep.FullName,
            Level = rep.Level,
            EmploymentStatus = rep.EmploymentStatus,
            TerritoryId = rep.TerritoryId,
            ReportingManagerId = rep.ReportingManagerId,
            Phone = rep.Phone,
            Email = rep.Email
        };
        await PopulateLookupsAsync(vm, excludeId: id);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Edit(int id, RepresentativeFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm, excludeId: id);
            return View(vm);
        }

        try
        {
            await repService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Representative updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesFull)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await repService.DeactivateAsync(id);
            TempData["StatusMessage"] = "Representative deactivated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateLookupsAsync(RepresentativeFormViewModel vm, int? excludeId = null)
    {
        vm.Territories = await db.Territories.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()))
            .ToListAsync();

        var managers = db.Representatives.AsNoTracking().AsQueryable();
        if (excludeId.HasValue) managers = managers.Where(r => r.Id != excludeId);

        vm.Managers = await managers
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();
    }

    private static RepresentativeSaveRequest ToRequest(RepresentativeFormViewModel vm) => new()
    {
        EmployeeCode = vm.EmployeeCode,
        FullName = vm.FullName,
        Level = vm.Level,
        EmploymentStatus = vm.EmploymentStatus,
        TerritoryId = vm.TerritoryId,
        ReportingManagerId = vm.ReportingManagerId,
        Phone = vm.Phone,
        Email = vm.Email
    };
}

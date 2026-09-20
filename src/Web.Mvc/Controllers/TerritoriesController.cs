using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Territories;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.RepresentativesView)]
public class TerritoriesController(ITerritoryService territoryService, IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var territories = await territoryService.GetListAsync();
        return View(territories);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            return View(await territoryService.GetByIdAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new TerritoryFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Create(TerritoryFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        int id;
        try
        {
            id = await territoryService.CreateAsync(ToRequest(vm));
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        TempData["StatusMessage"] = "Territory created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        TerritoryDetailDto territory;
        try
        {
            territory = await territoryService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new TerritoryFormViewModel
        {
            Id = territory.Id,
            Name = territory.Name,
            Region = territory.Region,
            Description = territory.Description,
            Type = territory.Type,
            ParentTerritoryId = territory.ParentTerritoryId,
            DistrictManagerId = territory.DistrictManagerId
        };
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesEdit)]
    public async Task<IActionResult> Edit(int id, TerritoryFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        try
        {
            await territoryService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        TempData["StatusMessage"] = "Territory updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RepresentativesFull)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await territoryService.DeleteAsync(id);
            TempData["StatusMessage"] = "Territory deleted.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLookupsAsync(TerritoryFormViewModel vm)
    {
        vm.Managers = await db.Representatives.AsNoTracking()
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();

        vm.ParentTerritories = await db.Territories.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Id != vm.Id)
            .OrderBy(t => t.Type).ThenBy(t => t.Name)
            .Select(t => new SelectListItem($"{t.Name} ({t.Type})", t.Id.ToString()))
            .ToListAsync();
    }

    private static TerritorySaveRequest ToRequest(TerritoryFormViewModel vm) => new()
    {
        Name = vm.Name,
        Region = vm.Region,
        Description = vm.Description,
        Type = vm.Type,
        ParentTerritoryId = vm.ParentTerritoryId,
        DistrictManagerId = vm.DistrictManagerId
    };
}

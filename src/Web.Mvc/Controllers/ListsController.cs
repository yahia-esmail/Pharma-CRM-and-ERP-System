using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Lists;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>Lists Management (gap-analysis addendum 3.1) — static/dynamic customer lists, bulk
/// reassignment, and the customer transfer log.</summary>
[Authorize(Policy = Policies.ListsView)]
public class ListsController(IListService listService, IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var lists = await listService.GetListsAsync();
        return View(lists);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var list = await listService.GetByIdAsync(id);
            ViewBag.AddMemberForm = list.Mode == CustomerListMode.Static ? await BuildAddMemberFormAsync(id) : null;
            ViewBag.BulkAssignForm = await BuildBulkAssignFormAsync(id);
            return View(list);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new ListFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Create(ListFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        var id = await listService.CreateAsync(ToRequest(vm));
        TempData["StatusMessage"] = "List created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        CustomerListDetailDto list;
        try
        {
            list = await listService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new ListFormViewModel
        {
            Id = list.Id,
            Name = list.Name,
            Type = list.Type,
            Mode = list.Mode,
            OwnerRepresentativeId = list.OwnerRepresentativeId,
            FilterTerritoryId = list.FilterTerritoryId,
            FilterClassificationId = list.FilterClassificationId,
            FilterSegment = list.FilterSegment,
            FilterActiveOnly = list.FilterActiveOnly
        };
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Edit(int id, ListFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        try
        {
            await listService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "List updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsFull)]
    public async Task<IActionResult> Delete(int id)
    {
        await listService.DeleteAsync(id);
        TempData["StatusMessage"] = "List deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> AddMember(int listId, int? doctorId, int? pharmacyId)
    {
        try
        {
            await listService.AddMemberAsync(listId, doctorId, pharmacyId);
            TempData["StatusMessage"] = "Customer added to the list.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = listId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> RemoveMember(int listId, int listItemId)
    {
        await listService.RemoveMemberAsync(listId, listItemId);
        TempData["StatusMessage"] = "Customer removed from the list.";
        return RedirectToAction(nameof(Details), new { id = listId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> BulkAssign(BulkAssignViewModel vm)
    {
        try
        {
            var result = await listService.BulkAssignAsync(vm.ListId, vm.ToRepresentativeId, vm.Reason);
            TempData["StatusMessage"] = $"Reassigned {result.AssignedCount} customer(s) " +
                $"({result.TransferLogCount} logged as a handover from a previous representative).";
        }
        catch (NotFoundException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = vm.ListId });
    }

    /// <summary>Standalone transfer entry point, also reached from a Doctor/Pharmacy's own Details page.</summary>
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Transfer(int? doctorId, int? pharmacyId)
    {
        if (doctorId is null && pharmacyId is null) return BadRequest();

        var vm = new CustomerTransferFormViewModel { DoctorId = doctorId, PharmacyId = pharmacyId };
        if (doctorId is { } dId)
        {
            var d = await db.Doctors.AsNoTracking().Include(x => x.PrimaryRepresentative)
                .FirstOrDefaultAsync(x => x.Id == dId);
            if (d is null) return NotFound();
            vm.CustomerName = d.FullName;
            vm.CurrentRepresentativeId = d.PrimaryRepresentativeId;
            vm.CurrentRepresentativeName = d.PrimaryRepresentative?.FullName;
        }
        else
        {
            var p = await db.Pharmacies.AsNoTracking().Include(x => x.PrimaryRepresentative)
                .FirstOrDefaultAsync(x => x.Id == pharmacyId);
            if (p is null) return NotFound();
            vm.CustomerName = p.Name;
            vm.CurrentRepresentativeId = p.PrimaryRepresentativeId;
            vm.CurrentRepresentativeName = p.PrimaryRepresentative?.FullName;
        }

        vm.Representatives = await BuildRepresentativeSelectListAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ListsEdit)]
    public async Task<IActionResult> Transfer(CustomerTransferFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Representatives = await BuildRepresentativeSelectListAsync();
            return View(vm);
        }

        try
        {
            await listService.TransferCustomerAsync(new CustomerTransferRequest
            {
                DoctorId = vm.DoctorId,
                PharmacyId = vm.PharmacyId,
                ToRepresentativeId = vm.ToRepresentativeId,
                Reason = vm.Reason
            });
            TempData["StatusMessage"] = "Customer transferred to the new representative.";
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            vm.Representatives = await BuildRepresentativeSelectListAsync();
            return View(vm);
        }

        return vm.DoctorId is { } doctorId
            ? RedirectToAction("Details", "Doctors", new { id = doctorId })
            : RedirectToAction("Details", "Pharmacies", new { id = vm.PharmacyId });
    }

    public async Task<IActionResult> Transfers(int? doctorId, int? pharmacyId)
    {
        var log = await listService.GetTransferLogAsync(doctorId, pharmacyId);
        return View(log);
    }

    private async Task<ListAddMemberViewModel> BuildAddMemberFormAsync(int listId)
    {
        return new ListAddMemberViewModel
        {
            ListId = listId,
            Doctors = await db.Doctors.AsNoTracking().OrderBy(d => d.FullName)
                .Select(d => new SelectListItem(d.FullName, d.Id.ToString())).ToListAsync(),
            Pharmacies = await db.Pharmacies.AsNoTracking().OrderBy(p => p.Name)
                .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync()
        };
    }

    private async Task<BulkAssignViewModel> BuildBulkAssignFormAsync(int listId) => new()
    {
        ListId = listId,
        Representatives = await BuildRepresentativeSelectListAsync()
    };

    private async Task<List<SelectListItem>> BuildRepresentativeSelectListAsync() =>
        await db.Representatives.AsNoTracking().OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString())).ToListAsync();

    private async Task PopulateLookupsAsync(ListFormViewModel vm)
    {
        vm.Representatives = await BuildRepresentativeSelectListAsync();
        vm.Territories = await db.Territories.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToListAsync();
        vm.Classifications = await db.DoctorClassifications.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
    }

    private static CustomerListSaveRequest ToRequest(ListFormViewModel vm) => new()
    {
        Name = vm.Name,
        Type = vm.Type,
        Mode = vm.Mode,
        OwnerRepresentativeId = vm.OwnerRepresentativeId,
        FilterTerritoryId = vm.FilterTerritoryId,
        FilterClassificationId = vm.FilterClassificationId,
        FilterSegment = vm.FilterSegment,
        FilterActiveOnly = vm.FilterActiveOnly
    };
}

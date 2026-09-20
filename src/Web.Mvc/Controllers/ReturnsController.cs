using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Returns;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>Formal Returns module (gap-analysis addendum 3.8) — Customer(Pharmacy)→Representative and
/// Representative→Warehouse flows, each requiring approval before it touches Stock Custody / Warehouse
/// Inventory.</summary>
[Authorize(Policy = Policies.CustodyView)]
public class ReturnsController(IReturnService returnService, IAppDbContext db, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(int? representativeId, ReturnStatus? status)
    {
        var repId = representativeId ?? currentUser.RepresentativeId;

        var vm = new ReturnsIndexViewModel
        {
            RepresentativeId = repId ?? 0,
            Returns = await returnService.GetListAsync(repId, status),
            Representatives = await RepresentativeOptionsAsync(),
            Pharmacies = await PharmacyOptionsAsync(),
            Warehouses = await WarehouseOptionsAsync(),
            Products = await ProductOptionsAsync()
        };

        if (repId.HasValue)
        {
            var rep = await db.Representatives.AsNoTracking().FirstOrDefaultAsync(r => r.Id == repId && !r.IsDeleted);
            vm.RepresentativeName = rep?.FullName;
            vm.NewRequest = new ReturnRequestViewModel { RepresentativeId = repId.Value };
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyReturn)]
    public async Task<IActionResult> RequestReturn([Bind(Prefix = "NewRequest")] ReturnRequestViewModel vm)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await returnService.RequestAsync(vm.RepresentativeId, currentUser.UserId, new ReturnRequestSaveRequest
            {
                FlowType = vm.FlowType,
                PharmacyId = vm.PharmacyId,
                WarehouseId = vm.WarehouseId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity,
                Reason = vm.Reason,
                Notes = vm.Notes
            });
            TempData["StatusMessage"] = "Return requested — awaiting approval.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = vm.RepresentativeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<IActionResult> Approve(int id, int representativeId)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await returnService.ApproveAsync(id, currentUser.UserId);
            TempData["StatusMessage"] = "Return approved and applied.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { representativeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<IActionResult> Reject(int id, int representativeId, string reason)
    {
        try
        {
            await returnService.RejectAsync(id, reason);
            TempData["StatusMessage"] = "Return rejected.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { representativeId });
    }

    private async Task<List<SelectListItem>> RepresentativeOptionsAsync() =>
        await db.Representatives.AsNoTracking().Where(r => !r.IsDeleted).OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> PharmacyOptionsAsync() =>
        await db.Pharmacies.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> WarehouseOptionsAsync() =>
        await db.Warehouses.AsNoTracking().Where(w => !w.IsDeleted).OrderBy(w => w.Name)
            .Select(w => new SelectListItem(w.Name, w.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> ProductOptionsAsync() =>
        await db.Products.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();
}

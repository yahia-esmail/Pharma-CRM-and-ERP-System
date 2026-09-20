using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Warehouses;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.CustodyView)]
public class CustodyController(ICustodyService custodyService, IWarehouseService warehouseService, IAppDbContext db,
    ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(int? representativeId)
    {
        var repId = representativeId ?? currentUser.RepresentativeId;
        if (repId is null)
        {
            if (!currentUser.HasUnrestrictedAccess && !User.IsInRole(Roles.Warehouse))
                return Forbid();

            // A manager/warehouse user with no rep of their own and no selection yet — show the picker only.
            var pickerVm = new CustodyIndexViewModel { Representatives = await RepresentativeOptionsAsync() };
            return View(pickerVm);
        }

        var rep = await db.Representatives.AsNoTracking().FirstOrDefaultAsync(r => r.Id == repId && !r.IsDeleted);
        if (rep is null) return NotFound();

        var vm = new CustodyIndexViewModel
        {
            RepresentativeId = rep.Id,
            RepresentativeName = rep.FullName,
            Balances = await custodyService.GetBalancesAsync(rep.Id),
            Ledger = await custodyService.GetLedgerAsync(rep.Id, null),
            Reservations = await custodyService.GetReservationsAsync(rep.Id),
            Issue = new IssueToCustodyViewModel { RepresentativeId = rep.Id },
            Return = new ReturnFromCustodyViewModel { RepresentativeId = rep.Id },
            Transfer = new CustodyTransferViewModel { FromRepresentativeId = rep.Id },
            Reconcile = new ReconciliationViewModel { RepresentativeId = rep.Id },
            Representatives = await RepresentativeOptionsAsync(),
            Warehouses = (await warehouseService.GetListAsync()).Select(w => new SelectListItem(w.Name, w.Id.ToString())),
            Products = await ProductOptionsAsync()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<IActionResult> Issue([Bind(Prefix = "Issue")] IssueToCustodyViewModel vm)
    {
        try
        {
            await custodyService.IssueAsync(new IssueToCustodyRequest
            {
                WarehouseId = vm.WarehouseId,
                RepresentativeId = vm.RepresentativeId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity,
                ReferenceNote = vm.ReferenceNote
            });
            TempData["StatusMessage"] = "Stock issued to custody.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = vm.RepresentativeId });
    }

    /// <summary>Direct, immediate return-to-warehouse — kept as a Warehouse/Admin correction tool.
    /// Representatives now go through the approval-gated Returns module (addendum 3.8) instead.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<IActionResult> Return([Bind(Prefix = "Return")] ReturnFromCustodyViewModel vm)
    {
        try
        {
            await custodyService.ReturnAsync(new ReturnFromCustodyRequest
            {
                RepresentativeId = vm.RepresentativeId,
                WarehouseId = vm.WarehouseId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity,
                ReasonCode = vm.ReasonCode
            });
            TempData["StatusMessage"] = "Return to warehouse recorded.";
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
    public async Task<IActionResult> Transfer([Bind(Prefix = "Transfer")] CustodyTransferViewModel vm)
    {
        try
        {
            await custodyService.TransferAsync(new CustodyTransferRequest
            {
                FromRepresentativeId = vm.FromRepresentativeId,
                ToRepresentativeId = vm.ToRepresentativeId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity
            });
            TempData["StatusMessage"] = "Custody transferred between representatives.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = vm.FromRepresentativeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<IActionResult> Reconcile([Bind(Prefix = "Reconcile")] ReconciliationViewModel vm)
    {
        var result = await custodyService.ReconcileAsync(new StockReconciliationRequest
        {
            RepresentativeId = vm.RepresentativeId,
            ProductId = vm.ProductId,
            ProductBatchId = vm.ProductBatchId,
            CountedBalance = vm.CountedBalance,
            Notes = vm.Notes
        });

        TempData["StatusMessage"] = result.Variance == 0
            ? "Reconciled — no variance found."
            : $"Reconciled — variance of {result.Variance:+#;-#;0} logged as an adjustment.";

        return RedirectToAction(nameof(Index), new { representativeId = vm.RepresentativeId });
    }

    private async Task<List<SelectListItem>> RepresentativeOptionsAsync() =>
        await db.Representatives.AsNoTracking().Where(r => !r.IsDeleted).OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> ProductOptionsAsync() =>
        await db.Products.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Purchasing;
using PharmaERP.Application.Warehouses;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.PurchaseOrdersView)]
public class PurchaseOrdersController(IPurchasingService purchasingService, IWarehouseService warehouseService,
    IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? supplierId, PurchaseOrderStatus? status, int page = 1)
    {
        var result = await purchasingService.GetListAsync(new PagedRequest { PageNumber = page }, supplierId, status);
        ViewBag.Status = status;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        PurchaseOrderDetailDto order;
        try
        {
            order = await purchasingService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new PurchaseOrderDetailsViewModel
        {
            Order = order,
            NewLine = new PurchaseOrderAddLineViewModel { PurchaseOrderId = id }
        };

        if (order.Status == PurchaseOrderStatus.Draft)
        {
            vm.Products = await db.Products.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name)
                .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();
        }

        if (order.Status is PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived)
        {
            vm.Receipt = new PurchaseReceiptFormViewModel
            {
                PurchaseOrderId = id,
                Warehouses = (await warehouseService.GetListAsync()).Select(w => new SelectListItem(w.Name, w.Id.ToString())),
                OrderLines = order.Lines.Where(l => l.QuantityReceived < l.Quantity)
                    .Select(l => new SelectListItem($"{l.ProductName} ({l.QuantityReceived}/{l.Quantity} received)", l.Id.ToString()))
            };
        }

        return View(vm);
    }

    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> Create(int? supplierId)
    {
        var vm = new PurchaseOrderCreateViewModel { SupplierId = supplierId ?? 0 };
        await PopulateSuppliersAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> Create(PurchaseOrderCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSuppliersAsync(vm);
            return View(vm);
        }

        var id = await purchasingService.CreateDraftAsync(new PurchaseOrderCreateRequest
        {
            SupplierId = vm.SupplierId,
            ExpectedDeliveryDate = vm.ExpectedDeliveryDate
        });
        TempData["StatusMessage"] = "Purchase order created as draft — add lines, then send.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> AddLine([Bind(Prefix = "NewLine")] PurchaseOrderAddLineViewModel vm)
    {
        try
        {
            await purchasingService.AddLineAsync(vm.PurchaseOrderId, new PurchaseOrderLineSaveRequest
            {
                ProductId = vm.ProductId,
                Quantity = vm.Quantity,
                UnitCost = vm.UnitCost
            });
            TempData["StatusMessage"] = "Line added.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.PurchaseOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> RemoveLine(int purchaseOrderId, int lineId)
    {
        try
        {
            await purchasingService.RemoveLineAsync(purchaseOrderId, lineId);
            TempData["StatusMessage"] = "Line removed.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = purchaseOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> Send(int id)
    {
        try
        {
            await purchasingService.SendAsync(id);
            TempData["StatusMessage"] = "Purchase order sent to supplier.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await purchasingService.CancelAsync(id);
            TempData["StatusMessage"] = "Purchase order cancelled.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PurchaseOrdersEdit)]
    public async Task<IActionResult> Receive([Bind(Prefix = "Receipt")] PurchaseReceiptFormViewModel vm)
    {
        try
        {
            await purchasingService.ReceiveAsync(new PurchaseReceiptRequest
            {
                PurchaseOrderId = vm.PurchaseOrderId,
                WarehouseId = vm.WarehouseId,
                Lines =
                [
                    new PurchaseReceiptLineRequest
                    {
                        PurchaseOrderLineId = vm.PurchaseOrderLineId,
                        Quantity = vm.Quantity,
                        BatchNumber = vm.BatchNumber,
                        ManufactureDate = vm.ManufactureDate,
                        ExpiryDate = vm.ExpiryDate
                    }
                ]
            });
            TempData["StatusMessage"] = "Goods receipt recorded and stock updated.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.PurchaseOrderId });
    }

    private async Task PopulateSuppliersAsync(PurchaseOrderCreateViewModel vm)
    {
        vm.Suppliers = await db.Suppliers.AsNoTracking().Where(s => !s.IsDeleted).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToListAsync();
    }
}

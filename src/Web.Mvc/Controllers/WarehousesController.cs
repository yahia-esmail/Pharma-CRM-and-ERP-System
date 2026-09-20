using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Warehouses;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.WarehousesView)]
public class WarehousesController(IWarehouseService warehouseService, IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await warehouseService.GetListAsync());

    [Authorize(Policy = Policies.WarehousesEdit)]
    public IActionResult Create() => View(new WarehouseFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Create(WarehouseFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await warehouseService.CreateAsync(new WarehouseSaveRequest { Name = vm.Name, Location = vm.Location, Type = vm.Type });
        TempData["StatusMessage"] = "Warehouse created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var warehouse = (await warehouseService.GetListAsync()).FirstOrDefault(w => w.Id == id);
        if (warehouse is null) return NotFound();

        return View(new WarehouseFormViewModel { Id = warehouse.Id, Name = warehouse.Name, Location = warehouse.Location, Type = warehouse.Type });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Edit(int id, WarehouseFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await warehouseService.UpdateAsync(id, new WarehouseSaveRequest { Name = vm.Name, Location = vm.Location, Type = vm.Type });
        TempData["StatusMessage"] = "Warehouse updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await warehouseService.DeleteAsync(id);
            TempData["StatusMessage"] = "Warehouse deleted.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> StockLevels(int? warehouseId, int? productId)
    {
        ViewBag.WarehouseId = warehouseId;
        ViewBag.ProductId = productId;
        ViewBag.Warehouses = await WarehouseOptionsAsync();
        ViewBag.Products = await ProductOptionsAsync();
        return View(await warehouseService.GetStockLevelsAsync(warehouseId, productId));
    }

    public async Task<IActionResult> Movements(int? warehouseId, int? productId, int page = 1)
    {
        ViewBag.WarehouseId = warehouseId;
        ViewBag.ProductId = productId;
        ViewBag.Warehouses = await WarehouseOptionsAsync();
        ViewBag.Products = await ProductOptionsAsync();
        return View(await warehouseService.GetMovementsAsync(new PagedRequest { PageNumber = page }, warehouseId, productId));
    }

    public async Task<IActionResult> Reports()
    {
        ViewBag.NearExpiry = await warehouseService.GetNearExpiryAsync(90);
        ViewBag.LowStock = await warehouseService.GetLowStockAsync();
        return View();
    }

    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Operations()
    {
        var vm = new WarehouseOperationsViewModel
        {
            Warehouses = await WarehouseOptionsAsync(),
            Products = await ProductOptionsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> GoodsReceipt([Bind(Prefix = "GoodsReceipt")] GoodsReceiptViewModel vm)
    {
        try
        {
            await warehouseService.ReceiveGoodsAsync(new GoodsReceiptRequest
            {
                WarehouseId = vm.WarehouseId,
                ProductId = vm.ProductId,
                BatchNumber = vm.BatchNumber,
                ManufactureDate = vm.ManufactureDate,
                ExpiryDate = vm.ExpiryDate,
                Quantity = vm.Quantity,
                ReferenceNote = vm.ReferenceNote
            });
            TempData["StatusMessage"] = "Goods receipt recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Operations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Transfer([Bind(Prefix = "Transfer")] StockTransferViewModel vm)
    {
        try
        {
            await warehouseService.TransferAsync(new StockTransferRequest
            {
                SourceWarehouseId = vm.SourceWarehouseId,
                DestinationWarehouseId = vm.DestinationWarehouseId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity,
                ReferenceNote = vm.ReferenceNote
            });
            TempData["StatusMessage"] = "Transfer recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Operations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> Adjust([Bind(Prefix = "Adjustment")] StockAdjustmentViewModel vm)
    {
        try
        {
            await warehouseService.AdjustAsync(new StockAdjustmentRequest
            {
                WarehouseId = vm.WarehouseId,
                ProductId = vm.ProductId,
                ProductBatchId = vm.ProductBatchId,
                Quantity = vm.Quantity,
                IsIncrease = vm.IsIncrease,
                ReasonCode = vm.ReasonCode
            });
            TempData["StatusMessage"] = "Adjustment recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Operations));
    }

    public async Task<IActionResult> Batches(int? productId)
    {
        ViewBag.ProductId = productId;
        ViewBag.Products = await ProductOptionsAsync();
        return View(await warehouseService.GetBatchesAsync(productId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.WarehousesEdit)]
    public async Task<IActionResult> CreateBatch(ProductBatchFormViewModel vm)
    {
        try
        {
            await warehouseService.CreateBatchAsync(new ProductBatchSaveRequest
            {
                ProductId = vm.ProductId,
                BatchNumber = vm.BatchNumber,
                ManufactureDate = vm.ManufactureDate,
                ExpiryDate = vm.ExpiryDate
            });
            TempData["StatusMessage"] = "Batch created.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Batches), new { productId = vm.ProductId });
    }

    private async Task<List<SelectListItem>> WarehouseOptionsAsync() =>
        (await warehouseService.GetListAsync()).Select(w => new SelectListItem(w.Name, w.Id.ToString())).ToList();

    private async Task<List<SelectListItem>> ProductOptionsAsync() =>
        await db.Products.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();
}

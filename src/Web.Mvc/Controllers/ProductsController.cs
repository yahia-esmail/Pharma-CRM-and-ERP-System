using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Products;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.ProductsView)]
public class ProductsController(IProductService productService) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await productService.GetListAsync(new PagedRequest { PageNumber = page, Search = search });
        ViewBag.Search = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            return View(await productService.GetByIdAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.ProductsEdit)]
    public IActionResult Create() => View(new ProductFormViewModel { UnitOfMeasure = "Box" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ProductsEdit)]
    public async Task<IActionResult> Create(ProductFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        try
        {
            var id = await productService.CreateAsync(ToRequest(vm));
            TempData["StatusMessage"] = "Product created successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }
    }

    [Authorize(Policy = Policies.ProductsEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        ProductDetailDto product;
        try
        {
            product = await productService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return View(new ProductFormViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            UnitOfMeasure = product.UnitOfMeasure,
            IsBatchTracked = product.IsBatchTracked,
            IsExpiryTracked = product.IsExpiryTracked,
            UnitCost = product.UnitCost,
            UnitPrice = product.UnitPrice,
            ReorderLevel = product.ReorderLevel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ProductsEdit)]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        try
        {
            await productService.UpdateAsync(id, ToRequest(vm));
            TempData["StatusMessage"] = "Product updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ProductsEdit)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await productService.DeactivateAsync(id);
            TempData["StatusMessage"] = "Product deactivated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    private static ProductSaveRequest ToRequest(ProductFormViewModel vm) => new()
    {
        Sku = vm.Sku,
        Name = vm.Name,
        Category = vm.Category,
        UnitOfMeasure = vm.UnitOfMeasure,
        IsBatchTracked = vm.IsBatchTracked,
        IsExpiryTracked = vm.IsExpiryTracked,
        UnitCost = vm.UnitCost,
        UnitPrice = vm.UnitPrice,
        ReorderLevel = vm.ReorderLevel
    };
}

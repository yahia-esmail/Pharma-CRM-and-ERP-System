using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Purchasing;
using PharmaERP.Application.Suppliers;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.SuppliersView)]
public class SuppliersController(ISupplierService supplierService, IPurchasingService purchasingService) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await supplierService.GetListAsync(new PagedRequest { PageNumber = page, Search = search });
        ViewBag.Search = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var supplier = await supplierService.GetByIdAsync(id);
            var ledger = await supplierService.GetLedgerAsync(id);
            var orders = await purchasingService.GetListAsync(new PagedRequest { PageSize = 50 }, id, null);

            return View(new SupplierDetailsViewModel
            {
                Supplier = supplier,
                Ledger = ledger,
                PurchaseOrders = orders.Items,
                NewPayment = new SupplierPaymentEntryViewModel { SupplierId = id }
            });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.SuppliersEdit)]
    public IActionResult Create() => View(new SupplierFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SuppliersEdit)]
    public async Task<IActionResult> Create(SupplierFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var id = await supplierService.CreateAsync(ToRequest(vm));
        TempData["StatusMessage"] = "Supplier created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.SuppliersEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        SupplierDetailDto s;
        try
        {
            s = await supplierService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return View(new SupplierFormViewModel
        {
            Id = s.Id, Name = s.Name, ContactName = s.ContactName, Phone = s.Phone, Email = s.Email,
            TaxRegistrationNumber = s.TaxRegistrationNumber, PaymentTermDays = s.PaymentTermDays, Status = s.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SuppliersEdit)]
    public async Task<IActionResult> Edit(int id, SupplierFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        try
        {
            await supplierService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Supplier updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SuppliersEdit)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await supplierService.DeactivateAsync(id);
            TempData["StatusMessage"] = "Supplier deactivated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SuppliersEdit)]
    public async Task<IActionResult> AddPayment([Bind(Prefix = "NewPayment")] SupplierPaymentEntryViewModel vm)
    {
        try
        {
            await supplierService.RecordPaymentAsync(new SupplierPaymentSaveRequest
            {
                SupplierId = vm.SupplierId,
                Amount = vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                ReferenceNumber = vm.ReferenceNumber,
                Notes = vm.Notes
            });
            TempData["StatusMessage"] = "Payment recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.SupplierId });
    }

    private static SupplierSaveRequest ToRequest(SupplierFormViewModel vm) => new()
    {
        Name = vm.Name,
        ContactName = vm.ContactName,
        Phone = vm.Phone,
        Email = vm.Email,
        TaxRegistrationNumber = vm.TaxRegistrationNumber,
        PaymentTermDays = vm.PaymentTermDays,
        Status = vm.Status
    };
}

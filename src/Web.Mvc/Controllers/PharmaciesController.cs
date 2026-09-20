using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Orders;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.PharmaciesView)]
public class PharmaciesController(IPharmacyService pharmacyService, IOrderService orderService, IAppDbContext db,
    ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await pharmacyService.GetListAsync(new PagedRequest { PageNumber = page, Search = search }, null, null);
        ViewBag.Search = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var pharmacy = await pharmacyService.GetByIdAsync(id);
            var visits = await pharmacyService.GetVisitsAsync(id);
            var ledger = await pharmacyService.GetLedgerAsync(id);
            var orders = await orderService.GetListAsync(new PagedRequest { PageSize = 50 }, id, null, null);

            return View(new PharmacyDetailsViewModel
            {
                Pharmacy = pharmacy,
                Visits = visits,
                Ledger = ledger,
                Orders = orders.Items,
                NewVisit = new PharmacyVisitFormViewModel { PharmacyId = id }
            });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.PharmaciesEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new PharmacyFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PharmaciesEdit)]
    public async Task<IActionResult> Create(PharmacyFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        var id = await pharmacyService.CreateAsync(ToRequest(vm));
        TempData["StatusMessage"] = "Pharmacy created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.PharmaciesEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        PharmacyDetailDto pharmacy;
        try
        {
            pharmacy = await pharmacyService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new PharmacyFormViewModel
        {
            Id = pharmacy.Id,
            Name = pharmacy.Name,
            LicenseNumber = pharmacy.LicenseNumber,
            OwnerName = pharmacy.OwnerName,
            Address = pharmacy.Address,
            Governorate = pharmacy.Governorate,
            City = pharmacy.City,
            Phone = pharmacy.Phone,
            WhatsAppNumber = pharmacy.WhatsAppNumber,
            Email = pharmacy.Email,
            Segment = pharmacy.Segment,
            PaymentTermDays = pharmacy.PaymentTermDays,
            CreditLimit = pharmacy.CreditLimit,
            PrimaryRepresentativeId = pharmacy.PrimaryRepresentativeId,
            TerritoryId = pharmacy.TerritoryId,
            Status = pharmacy.Status
        };
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PharmaciesEdit)]
    public async Task<IActionResult> Edit(int id, PharmacyFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        try
        {
            await pharmacyService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Pharmacy updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.PharmaciesFull)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await pharmacyService.DeactivateAsync(id);
            TempData["StatusMessage"] = "Pharmacy deactivated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVisit([Bind(Prefix = "NewVisit")] PharmacyVisitFormViewModel vm)
    {
        if (!ModelState.IsValid || currentUser.RepresentativeId is not { } repId)
        {
            TempData["ErrorMessage"] = "Unable to log the visit — check the required fields.";
            return RedirectToAction(nameof(Details), new { id = vm.PharmacyId });
        }

        try
        {
            await pharmacyService.AddVisitAsync(repId, new PharmacyVisitSaveRequest
            {
                PharmacyId = vm.PharmacyId,
                VisitDateUtc = vm.VisitDateUtc,
                DurationMinutes = vm.DurationMinutes,
                Purpose = vm.Purpose,
                Notes = vm.Notes,
                CheckInLatitude = vm.CheckInLatitude,
                CheckInLongitude = vm.CheckInLongitude
            });
            TempData["StatusMessage"] = "Visit logged.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.PharmacyId });
    }

    private async Task PopulateLookupsAsync(PharmacyFormViewModel vm)
    {
        vm.Representatives = await db.Representatives.AsNoTracking()
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();

        vm.Territories = await db.Territories.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()))
            .ToListAsync();
    }

    private static PharmacySaveRequest ToRequest(PharmacyFormViewModel vm) => new()
    {
        Name = vm.Name,
        LicenseNumber = vm.LicenseNumber,
        OwnerName = vm.OwnerName,
        Address = vm.Address,
        Governorate = vm.Governorate,
        City = vm.City,
        Phone = vm.Phone,
        WhatsAppNumber = vm.WhatsAppNumber,
        Email = vm.Email,
        Segment = vm.Segment,
        PaymentTermDays = vm.PaymentTermDays,
        CreditLimit = vm.CreditLimit,
        PrimaryRepresentativeId = vm.PrimaryRepresentativeId,
        TerritoryId = vm.TerritoryId,
        Status = vm.Status
    };
}

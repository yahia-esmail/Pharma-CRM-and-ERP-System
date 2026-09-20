using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Doctors;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.DoctorsView)]
public class DoctorsController(IDoctorService doctorService, IAppDbContext db, ICurrentUserService currentUser)
    : Controller
{
    public async Task<IActionResult> Index(string? search, string? specialty, string? city, int page = 1)
    {
        var result = await doctorService.GetListAsync(
            new PagedRequest { PageNumber = page, Search = search },
            specialty, city, null, null, null);

        return View(new DoctorIndexViewModel { Result = result, Search = search, Specialty = specialty, City = city });
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var doctor = await doctorService.GetByIdAsync(id);
            var visits = await doctorService.GetVisitsAsync(id);
            var followUps = await doctorService.GetFollowUpsAsync(id);

            return View(new DoctorDetailsViewModel
            {
                Doctor = doctor,
                Visits = visits,
                FollowUps = followUps,
                NewVisit = new DoctorVisitFormViewModel { DoctorId = id },
                NewFollowUp = new DoctorFollowUpFormViewModel { DoctorId = id, OwnerRepresentativeId = currentUser.RepresentativeId ?? 0 }
            });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.DoctorsEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new DoctorFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.DoctorsEdit)]
    public async Task<IActionResult> Create(DoctorFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        if (!vm.ConfirmDespiteDuplicates)
        {
            var duplicates = await doctorService.FindDuplicatesAsync(vm.FullName, vm.Phone, vm.ClinicOrHospital);
            if (duplicates.Count > 0)
            {
                await PopulateLookupsAsync(vm);
                ViewBag.Duplicates = duplicates;
                return View("ConfirmDuplicate", vm);
            }
        }

        var id = await doctorService.CreateAsync(ToRequest(vm));
        TempData["StatusMessage"] = "Doctor created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.DoctorsEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        DoctorDetailDto doctor;
        try
        {
            doctor = await doctorService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new DoctorFormViewModel
        {
            Id = doctor.Id,
            FullName = doctor.FullName,
            Specialty = doctor.Specialty,
            SubSpecialty = doctor.SubSpecialty,
            ClinicOrHospital = doctor.ClinicOrHospital,
            Address = doctor.Address,
            Governorate = doctor.Governorate,
            City = doctor.City,
            Phone = doctor.Phone,
            WhatsAppNumber = doctor.WhatsAppNumber,
            Email = doctor.Email,
            ClassificationId = doctor.ClassificationId,
            PreferredVisitingDays = doctor.PreferredVisitingDays,
            PreferredVisitingTimes = doctor.PreferredVisitingTimes,
            PrimaryRepresentativeId = doctor.PrimaryRepresentativeId,
            TerritoryId = doctor.TerritoryId,
            Status = doctor.Status,
            ConfirmDespiteDuplicates = true
        };
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.DoctorsEdit)]
    public async Task<IActionResult> Edit(int id, DoctorFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        try
        {
            await doctorService.UpdateAsync(id, ToRequest(vm));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Doctor updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.DoctorsFull)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await doctorService.DeactivateAsync(id);
            TempData["StatusMessage"] = "Doctor deactivated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVisit([Bind(Prefix = "NewVisit")] DoctorVisitFormViewModel vm)
    {
        if (!ModelState.IsValid || currentUser.RepresentativeId is not { } repId)
        {
            TempData["ErrorMessage"] = "Unable to log the visit — check the required fields.";
            return RedirectToAction(nameof(Details), new { id = vm.DoctorId });
        }

        try
        {
            await doctorService.AddVisitAsync(repId, new DoctorVisitSaveRequest
            {
                DoctorId = vm.DoctorId,
                VisitDateUtc = vm.VisitDateUtc,
                DurationMinutes = vm.DurationMinutes,
                ProductsDiscussed = vm.ProductsDiscussed,
                SamplesGiven = vm.SamplesGiven,
                MaterialsLeft = vm.MaterialsLeft,
                FeedbackNotes = vm.FeedbackNotes,
                NextVisitRecommendation = vm.NextVisitRecommendation,
                CheckInLatitude = vm.CheckInLatitude,
                CheckInLongitude = vm.CheckInLongitude
            });
            TempData["StatusMessage"] = "Visit logged.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.DoctorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFollowUp([Bind(Prefix = "NewFollowUp")] DoctorFollowUpFormViewModel vm)
    {
        if (!ModelState.IsValid || currentUser.RepresentativeId is not int ownerRepresentativeId)
        {
            TempData["ErrorMessage"] = "Unable to create the follow-up — check the required fields.";
            return RedirectToAction(nameof(Details), new { id = vm.DoctorId });
        }

        try
        {
            await doctorService.AddFollowUpAsync(new DoctorFollowUpSaveRequest
            {
                DoctorId = vm.DoctorId,
                DoctorVisitId = vm.DoctorVisitId,
                Type = vm.Type,
                DueDateUtc = vm.DueDateUtc,
                OwnerRepresentativeId = ownerRepresentativeId
            });
            TempData["StatusMessage"] = "Follow-up created.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.DoctorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseFollowUp(int followUpId, int doctorId, string? outcomeNotes)
    {
        try
        {
            await doctorService.CloseFollowUpAsync(followUpId, outcomeNotes);
            TempData["StatusMessage"] = "Follow-up closed.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = doctorId });
    }

    private async Task PopulateLookupsAsync(DoctorFormViewModel vm)
    {
        vm.Classifications = await db.DoctorClassifications.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        vm.Representatives = await db.Representatives.AsNoTracking()
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();

        vm.Territories = await db.Territories.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()))
            .ToListAsync();
    }

    private static DoctorSaveRequest ToRequest(DoctorFormViewModel vm) => new()
    {
        FullName = vm.FullName,
        Specialty = vm.Specialty,
        SubSpecialty = vm.SubSpecialty,
        ClinicOrHospital = vm.ClinicOrHospital,
        Address = vm.Address,
        Governorate = vm.Governorate,
        City = vm.City,
        Phone = vm.Phone,
        WhatsAppNumber = vm.WhatsAppNumber,
        Email = vm.Email,
        ClassificationId = vm.ClassificationId,
        PreferredVisitingDays = vm.PreferredVisitingDays,
        PreferredVisitingTimes = vm.PreferredVisitingTimes,
        PrimaryRepresentativeId = vm.PrimaryRepresentativeId,
        TerritoryId = vm.TerritoryId,
        Status = vm.Status
    };
}

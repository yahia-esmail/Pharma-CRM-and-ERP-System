using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.VisitPlansView)]
public class VisitPlansController(IVisitPlanService visitPlanService, IAppDbContext db, ICurrentUserService currentUser)
    : Controller
{
    public async Task<IActionResult> Index(int? representativeId, VisitPlanStatus? status, int page = 1)
    {
        var result = await visitPlanService.GetListAsync(
            new PagedRequest { PageNumber = page }, representativeId, status);

        ViewBag.RepresentativeId = representativeId;
        ViewBag.Status = status;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        VisitPlanDetailDto plan;
        try
        {
            plan = await visitPlanService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new VisitPlanDetailsViewModel
        {
            Plan = plan,
            NewItem = new VisitPlanAddItemViewModel { VisitPlanId = id, PlannedDate = plan.StartDate }
        };

        if (plan.Status != VisitPlanStatus.Draft)
            vm.PlanVsActual = await visitPlanService.GetPlanVsActualAsync(id);

        if (plan.Status is VisitPlanStatus.Draft or VisitPlanStatus.Rejected)
        {
            vm.Doctors = await db.Doctors.AsNoTracking()
                .Where(d => !d.IsDeleted)
                .OrderBy(d => d.FullName)
                .Select(d => new SelectListItem(d.FullName, d.Id.ToString()))
                .ToListAsync();
        }

        return View(vm);
    }

    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<IActionResult> Create()
    {
        var vm = new VisitPlanCreateViewModel();
        await PopulateRepresentativesAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<IActionResult> Create(VisitPlanCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRepresentativesAsync(vm);
            return View(vm);
        }

        try
        {
            var id = await visitPlanService.CreateDraftAsync(new VisitPlanCreateRequest
            {
                RepresentativeId = vm.RepresentativeId,
                PeriodType = vm.PeriodType,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate
            });
            TempData["StatusMessage"] = "Visit plan created as draft — add planned visits, then submit.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationFailedException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateRepresentativesAsync(vm);
            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<IActionResult> AddItem([Bind(Prefix = "NewItem")] VisitPlanAddItemViewModel vm)
    {
        try
        {
            await visitPlanService.AddItemAsync(vm.VisitPlanId, new VisitPlanItemSaveRequest
            {
                DoctorId = vm.DoctorId,
                PlannedDate = vm.PlannedDate,
                Sequence = vm.Sequence,
                Notes = vm.Notes
            });
            TempData["StatusMessage"] = "Planned visit added.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.VisitPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<IActionResult> RemoveItem(int visitPlanId, int itemId)
    {
        try
        {
            await visitPlanService.RemoveItemAsync(visitPlanId, itemId);
            TempData["StatusMessage"] = "Planned visit removed.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = visitPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.VisitPlansEdit)]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            await visitPlanService.SubmitAsync(id);
            TempData["StatusMessage"] = "Plan submitted for approval.";
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
    [Authorize(Policy = Policies.VisitPlansApprove)]
    public async Task<IActionResult> Approve(int id)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await visitPlanService.ApproveAsync(id, currentUser.UserId);
            TempData["StatusMessage"] = "Plan approved.";
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
    [Authorize(Policy = Policies.VisitPlansApprove)]
    public async Task<IActionResult> Reject(VisitPlanRejectViewModel vm)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await visitPlanService.RejectAsync(vm.VisitPlanId, currentUser.UserId, vm.Reason);
            TempData["StatusMessage"] = "Plan rejected and sent back to the representative.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = vm.VisitPlanId });
    }

    private async Task PopulateRepresentativesAsync(VisitPlanCreateViewModel vm)
    {
        vm.Representatives = await db.Representatives.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();
    }
}

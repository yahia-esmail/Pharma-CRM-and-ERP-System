using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.CollectionsView)]
public class CollectionsController(ICollectionService collectionService, IAppDbContext db, ICurrentUserService currentUser)
    : Controller
{
    public async Task<IActionResult> Index(int? representativeId)
    {
        var repId = representativeId ?? (currentUser.HasUnrestrictedAccess || User.IsInRole(Roles.Finance) ? null : currentUser.RepresentativeId);

        var vm = new FinancialCustodyIndexViewModel
        {
            RepresentativeId = repId ?? 0,
            Summary = await collectionService.GetFinancialCustodySummaryAsync(null),
            Representatives = await RepresentativeOptionsAsync(),
            Pharmacies = await PharmacyOptionsAsync()
        };

        if (repId.HasValue)
        {
            vm.Selected = await collectionService.GetFinancialCustodyAsync(repId.Value);
            vm.Collections = (await collectionService.GetCollectionsAsync(new PagedRequest { PageSize = 50 }, repId, null)).Items;
            vm.Remittances = (await collectionService.GetRemittancesAsync(new PagedRequest { PageSize = 50 }, repId)).Items;
            vm.Reconciliations = await collectionService.GetReconciliationsAsync(repId, null);
            vm.NewCollection = new CollectionEntryViewModel();
            vm.NewRemittance = new RemittanceEntryViewModel { RepresentativeId = repId.Value };
            vm.NewReconciliation = new FinancialReconciliationRequestViewModel { RepresentativeId = repId.Value };
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CollectionsCreate)]
    public async Task<IActionResult> AddCollection([Bind(Prefix = "NewCollection")] CollectionEntryViewModel vm, IFormFile? attachment)
    {
        if (currentUser.RepresentativeId is not { } repId)
        {
            TempData["ErrorMessage"] = "Only a representative account can record a collection.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var collectionId = await collectionService.RecordCollectionAsync(repId, new CollectionSaveRequest
            {
                PharmacyId = vm.PharmacyId,
                SaleId = vm.SaleId,
                Amount = vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                ReferenceNumber = vm.ReferenceNumber,
                Notes = vm.Notes
            });

            if (attachment is { Length: > 0 } && currentUser.UserId is not null)
            {
                await using var stream = attachment.OpenReadStream();
                await collectionService.AddAttachmentAsync(collectionId, currentUser.UserId, attachment.FileName,
                    attachment.ContentType, attachment.Length, stream);
            }

            TempData["StatusMessage"] = "Collection recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = repId });
    }

    public async Task<IActionResult> Attachments(int collectionId)
    {
        var collection = await db.Collections.AsNoTracking()
            .Include(c => c.Pharmacy).Include(c => c.Representative)
            .FirstOrDefaultAsync(c => c.Id == collectionId && !c.IsDeleted);
        if (collection is null) return NotFound();

        var vm = new CollectionAttachmentsViewModel
        {
            CollectionId = collectionId,
            RepresentativeId = collection.RepresentativeId,
            PharmacyName = collection.Pharmacy.Name,
            Amount = collection.Amount,
            CollectionDateUtc = collection.CollectionDateUtc,
            Attachments = await collectionService.GetAttachmentsAsync(collectionId)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CollectionsCreate)]
    public async Task<IActionResult> AddAttachment(int collectionId, IFormFile? attachment)
    {
        if (attachment is not { Length: > 0 } || currentUser.UserId is null)
        {
            TempData["ErrorMessage"] = "Choose a file to attach.";
            return RedirectToAction(nameof(Attachments), new { collectionId });
        }

        try
        {
            await using var stream = attachment.OpenReadStream();
            await collectionService.AddAttachmentAsync(collectionId, currentUser.UserId, attachment.FileName,
                attachment.ContentType, attachment.Length, stream);
            TempData["StatusMessage"] = "Attachment added.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Attachments), new { collectionId });
    }

    [Authorize(Policy = Policies.CollectionsView)]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        try
        {
            var (content, contentType, fileName) = await collectionService.OpenAttachmentAsync(id);
            return File(content, contentType, fileName);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.RemittanceConfirm)]
    public async Task<IActionResult> Remit([Bind(Prefix = "NewRemittance")] RemittanceEntryViewModel vm)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await collectionService.RemitAsync(currentUser.UserId, new RemittanceSaveRequest
            {
                RepresentativeId = vm.RepresentativeId,
                Amount = vm.Amount,
                RemittanceMethod = vm.RemittanceMethod,
                ReferenceNumber = vm.ReferenceNumber
            });
            TempData["StatusMessage"] = "Remittance confirmed — outstanding balance reduced.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = vm.RepresentativeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ReconciliationRequest)]
    public async Task<IActionResult> RequestReconciliation([Bind(Prefix = "NewReconciliation")] FinancialReconciliationRequestViewModel vm)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            var result = await collectionService.RequestReconciliationAsync(currentUser.UserId, new FinancialReconciliationRequest
            {
                RepresentativeId = vm.RepresentativeId,
                CountedBalance = vm.CountedBalance,
                Reason = vm.Reason
            });

            TempData["StatusMessage"] = result.Variance == 0
                ? $"Reconciled — no variance found (system balance: {result.SystemBalance:C})."
                : $"Difference of {result.Variance:C} logged — awaiting approval before it affects the outstanding balance.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { representativeId = vm.RepresentativeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ReconciliationApprove)]
    public async Task<IActionResult> ApproveReconciliation(int id, int representativeId)
    {
        if (currentUser.UserId is null) return Forbid();

        try
        {
            await collectionService.ApproveReconciliationAsync(id, currentUser.UserId);
            TempData["StatusMessage"] = "Reconciliation approved — the variance now counts toward the outstanding balance.";
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
    [Authorize(Policy = Policies.ReconciliationApprove)]
    public async Task<IActionResult> RejectReconciliation(int id, int representativeId, string reason)
    {
        try
        {
            await collectionService.RejectReconciliationAsync(id, reason);
            TempData["StatusMessage"] = "Reconciliation rejected — outstanding balance unaffected.";
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
}

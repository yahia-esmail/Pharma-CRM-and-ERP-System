using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Orders;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.OrdersView)]
public class OrdersController(IOrderService orderService, IAppDbContext db, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(int? pharmacyId, OrderStatus? status, int page = 1)
    {
        var result = await orderService.GetListAsync(new PagedRequest { PageNumber = page }, pharmacyId, null, status);
        ViewBag.PharmacyId = pharmacyId;
        ViewBag.Status = status;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        OrderDetailDto order;
        try
        {
            order = await orderService.GetByIdAsync(id);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var vm = new OrderDetailsViewModel
        {
            Order = order,
            NewLine = new OrderAddLineViewModel { OrderId = id }
        };

        if (order.Status == OrderStatus.Draft)
        {
            vm.Products = await db.Products.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem($"{p.Name} ({p.UnitPrice:C})", p.Id.ToString()))
                .ToListAsync();
        }

        return View(vm);
    }

    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> Create(int? pharmacyId)
    {
        var vm = new OrderCreateViewModel { PharmacyId = pharmacyId ?? 0 };
        await PopulatePharmaciesAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> Create(OrderCreateViewModel vm)
    {
        if (!ModelState.IsValid || currentUser.RepresentativeId is not { } repId)
        {
            await PopulatePharmaciesAsync(vm);
            if (currentUser.RepresentativeId is null)
                ModelState.AddModelError(string.Empty, "Only a representative account can create orders.");
            return View(vm);
        }

        var id = await orderService.CreateDraftAsync(repId, new OrderCreateRequest { PharmacyId = vm.PharmacyId });
        TempData["StatusMessage"] = "Order created as draft — add lines, then confirm.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> AddLine([Bind(Prefix = "NewLine")] OrderAddLineViewModel vm)
    {
        try
        {
            await orderService.AddLineAsync(vm.OrderId, new OrderLineSaveRequest
            {
                ProductId = vm.ProductId,
                Quantity = vm.Quantity,
                BonusQuantity = vm.BonusQuantity,
                DiscountPercent = vm.DiscountPercent
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

        return RedirectToAction(nameof(Details), new { id = vm.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> RemoveLine(int orderId, int lineId)
    {
        try
        {
            await orderService.RemoveLineAsync(orderId, lineId);
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

        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            await orderService.SubmitAsync(id);
            TempData["StatusMessage"] = "Order submitted for approval.";
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
    [Authorize(Policy = Policies.OrdersApprove)]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            await orderService.ApproveAsync(id);
            TempData["StatusMessage"] = "Order approved.";
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
    [Authorize(Policy = Policies.OrdersApprove)]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        try
        {
            await orderService.RejectAsync(id, reason);
            TempData["StatusMessage"] = "Order rejected.";
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
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> Deliver(int id)
    {
        try
        {
            await orderService.DeliverAsync(id);
            TempData["StatusMessage"] = "Order delivered and recorded as a sale.";
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
    [Authorize(Policy = Policies.OrdersEdit)]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await orderService.CancelAsync(id);
            TempData["StatusMessage"] = "Order cancelled.";
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

    private async Task PopulatePharmaciesAsync(OrderCreateViewModel vm)
    {
        vm.Pharmacies = await db.Pharmacies.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();
    }
}

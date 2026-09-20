using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>In-app notification center (addendum 3.10) — every notification is personal to its
/// recipient, so no role policy beyond being signed in is needed; each action scopes to the current user.</summary>
[Authorize]
public class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        var notifications = await notificationService.GetForUserAsync(userId, unreadOnly: false, take: 200);
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        if (currentUser.UserId is not { } userId) return Ok(0);

        return Ok(await notificationService.GetUnreadCountAsync(userId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (currentUser.UserId is { } userId)
            await notificationService.MarkReadAsync(id, userId);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        if (currentUser.UserId is { } userId)
            await notificationService.MarkAllReadAsync(userId);

        return RedirectToAction(nameof(Index));
    }
}

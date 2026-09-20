using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>In-app notification center for the mobile app (addendum 3.10) — every notification is
/// personal to its recipient, so no role policy beyond being signed in is needed.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromQuery] bool unreadOnly = false, [FromQuery] int take = 200, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        return Ok(await notificationService.GetForUserAsync(userId, unreadOnly, take, ct));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Ok(0);

        return Ok(await notificationService.GetUnreadCountAsync(userId, ct));
    }

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult> MarkRead(int id, CancellationToken ct)
    {
        if (currentUser.UserId is { } userId)
            await notificationService.MarkReadAsync(id, userId, ct);

        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<ActionResult> MarkAllRead(CancellationToken ct)
    {
        if (currentUser.UserId is { } userId)
            await notificationService.MarkAllReadAsync(userId, ct);

        return NoContent();
    }
}

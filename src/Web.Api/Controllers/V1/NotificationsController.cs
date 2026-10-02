using PharmaERP.Application.Common;
using PharmaERP.Infrastructure.Notifications;
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
public class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser,
    IPushSubscriptionService pushSubscriptions, VapidKeyProvider vapid) : ControllerBase
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

    // ---- Web Push (plan 10.4) ---------------------------------------------------------------------------

    /// <summary>The VAPID public key the browser needs to subscribe (it is public by design).</summary>
    [HttpGet("push/public-key")]
    public ActionResult<PushPublicKeyDto> GetPushPublicKey() => Ok(new PushPublicKeyDto(vapid.PublicKey));

    /// <summary>Registers this browser for push. Idempotent: the same endpoint is updated, not duplicated.</summary>
    [HttpPost("push-subscriptions")]
    public async Task<ActionResult> Subscribe(PushSubscriptionRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        try
        {
            await pushSubscriptions.SubscribeAsync(userId, request, ct);
            return NoContent();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpDelete("push-subscriptions")]
    public async Task<ActionResult> Unsubscribe(PushUnsubscribeRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        await pushSubscriptions.UnsubscribeAsync(userId, request.Endpoint, ct);
        return NoContent();
    }
}

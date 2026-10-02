using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>A browser's Web Push subscription (field app, plan 10.4) — where to deliver notifications to one
/// installed app on one device. A user can have several (phone + tablet). Removed when the push service
/// reports it gone (404/410) or the rep turns notifications off.</summary>
public class PushSubscription : BaseEntity
{
    public string UserId { get; set; } = null!;

    /// <summary>The push service URL (FCM, Mozilla, Apple…). Long; looked up via <see cref="EndpointHash"/>.</summary>
    public string Endpoint { get; set; } = null!;
    public string EndpointHash { get; set; } = null!;

    /// <summary>The browser's keys for encrypting the payload (Base64url).</summary>
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastDeliveredAtUtc { get; set; }
}

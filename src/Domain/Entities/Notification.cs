using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>One in-app notification (gap-analysis addendum 3.10). High-volume, system-generated, and
/// never edited after creation — deliberately a plain BaseEntity (no soft-delete/audit trail) rather than
/// AuditableEntity, the same choice already made for LocationPing.</summary>
public class Notification : BaseEntity
{
    public string RecipientUserId { get; set; } = null!;

    /// <summary>Free-form trigger key (see NotificationTypes) rather than an enum — addendum 3.10
    /// explicitly calls for a trigger catalog "extensible via configuration, not hard-coded."</summary>
    public string Type { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

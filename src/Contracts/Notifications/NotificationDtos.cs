namespace PharmaERP.Application.Notifications;

public record NotificationDto(
    int Id,
    string Type,
    string Message,
    string? RelatedEntityType,
    int? RelatedEntityId,
    bool IsRead,
    DateTime CreatedAtUtc);

/// <summary>A browser PushSubscription as JSON.stringify(subscription) produces it.</summary>
public class PushSubscriptionRequest
{
    public string Endpoint { get; set; } = null!;
    public PushSubscriptionKeys Keys { get; set; } = new();
}

public class PushSubscriptionKeys
{
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;
}

public class PushUnsubscribeRequest
{
    public string Endpoint { get; set; } = null!;
}

public record PushPublicKeyDto(string PublicKey);

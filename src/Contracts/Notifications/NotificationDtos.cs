namespace PharmaERP.Application.Notifications;

public record NotificationDto(
    int Id,
    string Type,
    string Message,
    string? RelatedEntityType,
    int? RelatedEntityId,
    bool IsRead,
    DateTime CreatedAtUtc);

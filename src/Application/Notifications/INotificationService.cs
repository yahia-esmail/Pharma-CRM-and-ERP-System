namespace PharmaERP.Application.Notifications;

public interface INotificationService
{
    Task<int> CreateAsync(string recipientUserId, string type, string message, string? relatedEntityType = null,
        int? relatedEntityId = null, CancellationToken ct = default);

    /// <summary>Same as CreateAsync, but skips creating a duplicate if this exact
    /// (recipient, type, relatedEntityId) combination already has a notification from today — for
    /// background-scan triggers that re-check on every scan interval and shouldn't re-notify each time.</summary>
    Task CreateIfNotAlreadyNotifiedTodayAsync(string recipientUserId, string type, string message,
        string? relatedEntityType, int? relatedEntityId, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(string userId, bool unreadOnly, int take, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);

    Task MarkReadAsync(int notificationId, string userId, CancellationToken ct = default);

    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
}

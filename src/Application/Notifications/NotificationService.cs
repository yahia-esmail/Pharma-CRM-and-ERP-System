using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Notifications;

public class NotificationService(IAppDbContext db, IBusinessCalendar calendar, IPushNotifier? push = null) : INotificationService
{
    public async Task<int> CreateAsync(string recipientUserId, string type, string message, string? relatedEntityType = null,
        int? relatedEntityId = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Type = type,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        push?.Enqueue(new PushMessage(recipientUserId, notification.Id, type, message, relatedEntityType, relatedEntityId));
        return notification.Id;
    }

    public async Task CreateIfNotAlreadyNotifiedTodayAsync(string recipientUserId, string type, string message,
        string? relatedEntityType, int? relatedEntityId, CancellationToken ct = default)
    {
        var todayStartUtc = calendar.StartOfDayUtc(calendar.Today);
        var alreadyNotified = await db.Notifications.AsNoTracking().AnyAsync(n =>
            n.RecipientUserId == recipientUserId && n.Type == type && n.RelatedEntityId == relatedEntityId
            && n.CreatedAtUtc >= todayStartUtc, ct);

        if (!alreadyNotified)
            await CreateAsync(recipientUserId, type, message, relatedEntityType, relatedEntityId, ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(string userId, bool unreadOnly, int take, CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.RelatedEntityType, n.RelatedEntityId, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        return await db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId && !n.IsRead, ct);
    }

    public async Task MarkReadAsync(int notificationId, string userId, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct);
        if (notification is null) return;

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        await db.Notifications.Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}

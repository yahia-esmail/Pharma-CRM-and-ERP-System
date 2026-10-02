using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Notifications;

/// <summary>A notification to deliver by Web Push, right after it was stored.</summary>
public sealed record PushMessage(string RecipientUserId, int NotificationId, string Type, string Message,
    string? RelatedEntityType, int? RelatedEntityId);

/// <summary>Hands a stored notification over for push delivery. Never blocks or fails the caller: delivery runs
/// in the background, and a notification is in the app's list whether or not the push gets through.</summary>
public interface IPushNotifier
{
    void Enqueue(PushMessage message);
}

public interface IPushSubscriptionService
{
    Task SubscribeAsync(string userId, PushSubscriptionRequest request, CancellationToken ct = default);
    Task UnsubscribeAsync(string userId, string endpoint, CancellationToken ct = default);
}

public class PushSubscriptionService(IAppDbContext db, TimeProvider time) : IPushSubscriptionService
{
    public async Task SubscribeAsync(string userId, PushSubscriptionRequest request, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new ValidationFailedException("The push endpoint must be an https URL.");
        if (string.IsNullOrWhiteSpace(request.Keys?.P256dh) || string.IsNullOrWhiteSpace(request.Keys.Auth))
            throw new ValidationFailedException("The subscription keys are missing.");

        var hash = Hash(request.Endpoint);
        // One endpoint = one browser install. If another user signed in on it before, it now belongs to this one.
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.EndpointHash == hash, ct);
        if (existing is null)
        {
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId, Endpoint = request.Endpoint, EndpointHash = hash,
                P256dh = request.Keys.P256dh, Auth = request.Keys.Auth, CreatedAtUtc = time.GetUtcNow().UtcDateTime
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = request.Keys.P256dh;
            existing.Auth = request.Keys.Auth;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(string userId, string endpoint, CancellationToken ct = default)
    {
        var hash = Hash(endpoint);
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.EndpointHash == hash && s.UserId == userId, ct);
        if (existing is null) return;
        db.PushSubscriptions.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    public static string Hash(string endpoint) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
}

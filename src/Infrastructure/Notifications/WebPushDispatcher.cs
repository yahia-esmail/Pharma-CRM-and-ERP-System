using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;
using PharmaERP.Domain.Entities;
using WebPush;

namespace PharmaERP.Infrastructure.Notifications;

public class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>Contact for push services, e.g. "mailto:it@company.com" (required by the VAPID spec).</summary>
    public string Subject { get; set; } = "mailto:admin@pharmaerp.local";

    /// <summary>VAPID keys (Base64url). In production set them from a secret store, never appsettings.json in git.
    /// When empty, a key pair is generated once and kept in .keys/vapid.json (git-ignored), shared by the API and
    /// the dashboard.</summary>
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
}

/// <summary>The VAPID key pair: from configuration, else the generated one kept beside the Data Protection keys.</summary>
public sealed class VapidKeyProvider
{
    private readonly Lazy<VapidDetails> _details;

    public VapidKeyProvider(IOptions<WebPushOptions> options, IHostEnvironment env, ILogger<VapidKeyProvider> logger)
    {
        _details = new(() =>
        {
            var o = options.Value;
            if (!string.IsNullOrWhiteSpace(o.PublicKey) && !string.IsNullOrWhiteSpace(o.PrivateKey))
                return new VapidDetails(o.Subject, o.PublicKey, o.PrivateKey);

            var file = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", ".keys", "vapid.json"));
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var keys = VapidHelper.GenerateVapidKeys();
                try
                {
                    // CreateNew: if the API and the dashboard start together, only one key pair wins.
                    using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write);
                    JsonSerializer.Serialize(stream, new StoredKeys(keys.PublicKey, keys.PrivateKey));
                    logger.LogWarning("Generated a Web Push (VAPID) key pair in {File}. Configure WebPush:PublicKey/PrivateKey from a secret store in production.", file);
                }
                catch (IOException) when (File.Exists(file)) { }
            }
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(file))!;
            return new VapidDetails(o.Subject, stored.PublicKey, stored.PrivateKey);
        });
    }

    public string PublicKey => _details.Value.PublicKey;
    public VapidDetails Details => _details.Value;

    private sealed record StoredKeys(string PublicKey, string PrivateKey);
}

/// <summary>Queues stored notifications for Web Push delivery (see <see cref="WebPushDispatchService"/>).</summary>
public sealed class WebPushQueue : IPushNotifier
{
    private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<PushMessage> Reader => _channel.Reader;

    public void Enqueue(PushMessage message) => _channel.Writer.TryWrite(message);
}

/// <summary>Delivers queued notifications to every device the recipient subscribed, off the request path. A
/// subscription the push service no longer knows (404/410) is deleted; other failures are only logged — the
/// notification itself is safe in the database and shows in the app's list either way.</summary>
public sealed class WebPushDispatchService(WebPushQueue queue, IServiceScopeFactory scopes, VapidKeyProvider vapid,
    ILogger<WebPushDispatchService> logger) : BackgroundService
{
    private readonly WebPushClient _client = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Push for notification {Id} failed", message.NotificationId);
            }
        }
    }

    private async Task DeliverAsync(PushMessage message, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var subscriptions = await db.PushSubscriptions.Where(s => s.UserId == message.RecipientUserId).ToListAsync(ct);
        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            title = PushTitles.For(message.Type),
            body = message.Message,
            notificationId = message.NotificationId,
            type = message.Type,
            entityType = message.RelatedEntityType,
            entityId = message.RelatedEntityId
        });

        foreach (var s in subscriptions)
        {
            try
            {
                await _client.SendNotificationAsync(new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid.Details, ct);
                s.LastDeliveredAtUtc = DateTime.UtcNow;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                db.PushSubscriptions.Remove(s);   // uninstalled / permission revoked
            }
            catch (WebPushException ex)
            {
                logger.LogInformation("Push to {Host} failed: {Status}", new Uri(s.Endpoint).Host, ex.StatusCode);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}

public static class PushTitles
{
    public static string For(string type) => type switch
    {
        NotificationTypes.OrderApproved => "Order approved",
        NotificationTypes.OrderRejected => "Order rejected",
        NotificationTypes.ExpenseApproved => "Expense approved",
        NotificationTypes.ExpenseRejected => "Expense rejected",
        NotificationTypes.ReturnApproved => "Return approved",
        NotificationTypes.ReturnRejected => "Return rejected",
        NotificationTypes.PlannedVisitNotLogged => "Planned visit not logged",
        NotificationTypes.NearExpiry => "Stock near expiry",
        NotificationTypes.CustodyBalanceAging => "Cash custody reminder",
        NotificationTypes.CollectionAwaitingReview => "Collection awaiting review",
        _ => "Field CRM"
    };
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PharmaERP.Application.Notifications;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Notifications;

/// <summary>Where a notification takes the rep (wireframe 6 → the related record's screen).</summary>
public static class NotificationLinks
{
    public static string For(string? type, string? entityType, int? entityId) => (type, entityType) switch
    {
        (_, "Order") when entityId is { } id => $"orders/{id}/edit",
        (_, "Expense") => "expenses",
        (_, "ReturnTransaction") => "returns",
        (_, "Collection") or ("CustodyBalanceAging", _) => "custody?tab=financial",
        ("NearExpiry", _) or (_, "ProductBatch") => "custody",
        ("PlannedVisitNotLogged", _) or (_, "VisitPlan") or (_, "VisitPlanItem") => "plan",
        _ => "notifications"
    };

    public static string Icon(string type) => type switch
    {
        _ when type.EndsWith("Approved") => "ok",
        _ when type.EndsWith("Rejected") => "bad",
        "NearExpiry" or "CustodyBalanceAging" or "PlannedVisitNotLogged" => "warn",
        _ => "info"
    };
}

/// <summary>The rep's notifications (wireframe 6): cached for offline, unread count for the bottom-nav badge.
/// Marking read goes through the outbox, so it works without signal and the badge drops at once.</summary>
public sealed class NotificationCenter(NotificationsApi api, KeyValueStore storage, Outbox outbox, TimeProvider time,
    ILogger<NotificationCenter> logger)
{
    public const string ReadKind = "NotificationRead";
    public const string ReadAllKind = "NotificationsReadAll";

    private Cached<IReadOnlyList<NotificationDto>>? _cache;
    private bool _loaded;
    private int? _serverUnread;

    public event Action? Changed;

    public IReadOnlyList<NotificationDto> Items => _cache?.Data ?? [];
    public DateTime? SavedAtUtc => _cache?.SavedAtUtc;

    /// <summary>Unread, counting reads made on the phone that haven't synced yet.</summary>
    public int UnreadCount => _cache is null ? _serverUnread ?? 0 : Items.Count(n => !n.IsRead);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _cache = await storage.GetAsync<Cached<IReadOnlyList<NotificationDto>>>(StorageKeys.Notifications);
        _loaded = true;
        await ApplyPendingReadsAsync();
    }

    /// <summary>Re-downloads the list; false when the server couldn't be reached (the cached list stays).</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        try
        {
            _cache = new Cached<IReadOnlyList<NotificationDto>>(await api.GetMineAsync(ct), time.GetUtcNow().UtcDateTime);
            await storage.SetAsync(StorageKeys.Notifications, _cache);
            await ApplyPendingReadsAsync();
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation("Notifications not refreshed: {Error}", ex.Message);
            return false;
        }
    }

    public async Task MarkReadAsync(int id)
    {
        await EnsureLoadedAsync();
        if (Items.FirstOrDefault(n => n.Id == id) is { IsRead: true }) return;
        await outbox.EnqueueAsync(new OutboxRequest(ReadKind, $"Mark notification #{id} read", "POST", $"api/v1/Notifications/{id}/read"));
        await SetReadLocallyAsync(n => n.Id == id);
    }

    public async Task MarkAllReadAsync()
    {
        await EnsureLoadedAsync();
        if (UnreadCount == 0) return;
        await outbox.EnqueueAsync(new OutboxRequest(ReadAllKind, "Mark all notifications read", "POST", "api/v1/Notifications/mark-all-read"));
        await SetReadLocallyAsync(_ => true);
    }

    public void Reset()
    {
        _cache = null;
        _loaded = false;
        _serverUnread = null;
        Changed?.Invoke();
    }

    /// <summary>A freshly downloaded list doesn't know about reads still in the outbox — re-apply them.</summary>
    private async Task ApplyPendingReadsAsync()
    {
        var items = await outbox.GetItemsAsync();
        if (items.Any(i => i.Kind == ReadAllKind)) { await SetReadLocallyAsync(_ => true, notify: false); return; }
        var ids = items.Where(i => i.Kind == ReadKind)
            .Select(i => int.TryParse(i.Url.Split('/')[^2], out var id) ? id : 0).ToHashSet();
        if (ids.Count > 0) await SetReadLocallyAsync(n => ids.Contains(n.Id), notify: false);
    }

    private async Task SetReadLocallyAsync(Func<NotificationDto, bool> which, bool notify = true)
    {
        if (_cache is null) return;
        _cache = _cache with { Data = _cache.Data.Select(n => which(n) ? n with { IsRead = true } : n).ToList() };
        await storage.SetAsync(StorageKeys.Notifications, _cache);
        if (notify) Changed?.Invoke();
    }
}

public sealed record PushStatus(
    [property: JsonPropertyName("supported")] bool Supported,
    [property: JsonPropertyName("permission")] string Permission,
    [property: JsonPropertyName("subscribed")] bool Subscribed,
    [property: JsonPropertyName("standalone")] bool Standalone,
    [property: JsonPropertyName("ios")] bool Ios)
{
    /// <summary>iOS only delivers Web Push to an app added to the Home Screen (iOS 16.4+).</summary>
    public bool NeedsInstallFirst => Ios && !Standalone;
}

/// <summary>Web Push on this device (plan 10.4): turn on/off, and route taps on a notification into the app.</summary>
public sealed class PushService(IJSRuntime js, NotificationsApi api, ILogger<PushService> logger) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;
    private DotNetObjectReference<PushService>? _ref;

    /// <summary>A notification arrived while the app is open.</summary>
    public event Action? PushReceived;

    /// <summary>The rep tapped a notification while the app was open: navigate to this path.</summary>
    public event Action<string>? OpenRequested;

    private Task<IJSObjectReference> Module()
    {
        if (_module is null || _module.IsFaulted || _module.IsCanceled)
            _module = js.InvokeAsync<IJSObjectReference>("import", "./_content/PharmaERP.FieldApp.UI/js/push.js").AsTask();
        return _module;
    }

    public async Task StartListeningAsync()
    {
        try
        {
            _ref ??= DotNetObjectReference.Create(this);
            await (await Module()).InvokeVoidAsync("listen", _ref);
        }
        catch (JSException ex) { logger.LogInformation("Push listener not started: {Error}", ex.Message); }
    }

    public async Task<PushStatus> GetStatusAsync()
    {
        try { return await (await Module()).InvokeAsync<PushStatus>("status"); }
        catch (JSException) { return new PushStatus(false, "unsupported", false, false, false); }
    }

    /// <summary>Asks permission and registers this device with the API. Returns null on success, else why not.</summary>
    public async Task<string?> EnableAsync()
    {
        try
        {
            var key = await api.GetPushPublicKeyAsync();
            var result = await (await Module()).InvokeAsync<JsonElement>("subscribe", key);
            if (!result.GetProperty("ok").GetBoolean())
                return result.GetProperty("error").GetString() switch
                {
                    "denied" => "Notifications are blocked for this app. Allow them in the phone's settings for this site.",
                    "default" => "Notifications weren't allowed.",
                    "unsupported" => "This browser can't receive notifications.",
                    var other => $"Couldn't turn on notifications ({other})."
                };
            await api.SubscribePushAsync(result.GetProperty("json").GetString()!);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JSException)
        {
            return "Couldn't turn on notifications — check the connection and try again.";
        }
    }

    /// <summary>Stops notifications on this device (also on sign-out, so the next user doesn't get them).</summary>
    public async Task DisableAsync()
    {
        try
        {
            if (await (await Module()).InvokeAsync<string?>("unsubscribe") is { } endpoint)
                await api.UnsubscribePushAsync(endpoint);
        }
        catch (Exception ex) when (ex is HttpRequestException or JSException)
        {
            logger.LogInformation("Push unsubscribe incomplete: {Error}", ex.Message);
        }
    }

    [JSInvokable] public void OnPushReceived() => PushReceived?.Invoke();
    [JSInvokable] public void OnOpenPath(string path) => OpenRequested?.Invoke(path);

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true }) await _module.Result.DisposeAsync();
        _ref?.Dispose();
    }
}

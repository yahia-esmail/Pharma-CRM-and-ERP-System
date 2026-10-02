using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Orders;

/// <summary>Order entry on the phone (plan phase 7). Drafts live on the phone while being typed; Save draft
/// and Submit each send the whole order as ONE outbox request:
///
///   new order      POST api/v1/Orders        { pharmacyId, lines[], submit, pharmacyVisitId?, location? }
///   server draft   PUT  api/v1/Orders/{id}   same body — replaces every line
///
/// so a dropped connection can never leave a half-built order on the server. Taken during a pharmacy visit,
/// the order carries that visit's outbox ref, resolved to the server's visit id when it's sent.</summary>
public sealed class OrderEntryManager(KeyValueStore storage, Outbox outbox, IOutboxStore outboxStore, TimeProvider time)
{
    public const string OutboxKind = "OrderSave";
    public const string CancelKind = "OrderCancel";

    private List<OrderDraft>? _drafts;
    private List<PendingOrder>? _pending;

    public event Action? Changed;

    // ---- Drafts on the phone ------------------------------------------------------------------------

    public async Task<IReadOnlyList<OrderDraft>> GetDraftsAsync() => await LoadDraftsAsync();

    public async Task<OrderDraft?> GetDraftAsync(Guid localId) =>
        (await LoadDraftsAsync()).FirstOrDefault(d => d.LocalId == localId);

    /// <summary>The phone draft already open for this server order, if the rep started editing it before.</summary>
    public async Task<OrderDraft?> FindDraftForServerOrderAsync(int serverId) =>
        (await LoadDraftsAsync()).FirstOrDefault(d => d.ServerId == serverId);

    public async Task SaveLocalAsync(OrderDraft draft)
    {
        var drafts = await LoadDraftsAsync();
        draft.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
        drafts.RemoveAll(d => d.LocalId == draft.LocalId);
        drafts.Insert(0, draft);
        await storage.SetAsync(StorageKeys.OrderDrafts, drafts);
        Changed?.Invoke();
    }

    public async Task DiscardLocalAsync(Guid localId)
    {
        var drafts = await LoadDraftsAsync();
        if (drafts.RemoveAll(d => d.LocalId == localId) == 0) return;
        await storage.SetAsync(StorageKeys.OrderDrafts, drafts);
        Changed?.Invoke();
    }

    // ---- Sending --------------------------------------------------------------------------------------

    /// <summary>Queues the order for the server — as a draft, or submitted for approval — and takes it off the
    /// phone's draft list. <paramref name="location"/> is the rep's position at submit, when available.</summary>
    public async Task<PendingOrder> SendAsync(OrderDraft draft, bool submit, GeoFix? location)
    {
        if (draft.PharmacyId <= 0) throw new InvalidOperationException("Choose the pharmacy first.");
        if (submit && draft.IsEmpty) throw new InvalidOperationException("Add at least one product before submitting.");

        // Only link the visit if its server id is (or will be) known: a visit cancelled before its check-in was
        // sent never gets one, and the order must not get stuck waiting for it.
        var linkVisit = draft.VisitLocalId is { } visitId && await VisitResolvableAsync(visitId);

        var body = new
        {
            pharmacyId = draft.PharmacyId,
            lines = draft.Lines.Select(l => new
            {
                productId = l.ProductId,
                quantity = l.Quantity,
                bonusQuantity = l.BonusQuantity,
                discountPercent = l.DiscountPercent
            }),
            submit,
            // Replaced by the visit's server id before sending (the check-in is always queued first).
            pharmacyVisitId = linkVisit ? OutboxRefs.Placeholder($"visit:{draft.VisitLocalId}") : null,
            location = submit && location is { } fix
                ? new { latitude = fix.Latitude, longitude = fix.Longitude, accuracyMeters = Math.Round(fix.AccuracyMeters, 1), deviceTimestampUtc = fix.DeviceTimestampUtc }
                : null
        };

        var totals = draft.Totals;
        var item = await outbox.EnqueueAsync(new OutboxRequest(
            Kind: OutboxKind,
            Title: $"{(submit ? "Order" : "Draft order")} · {draft.PharmacyName} · {draft.Lines.Count} line(s)",
            Method: draft.ServerId is null ? "POST" : "PUT",
            Url: draft.ServerId is { } id ? $"api/v1/Orders/{id}" : "api/v1/Orders",
            Body: body,
            ProducesRef: draft.ServerId is null ? OrderRef(draft.LocalId) : null));

        var pending = new PendingOrder(draft.LocalId, item.Id, draft.ServerId, draft.PharmacyName, draft.Lines.Count,
            totals.Net, submit, time.GetUtcNow().UtcDateTime, draft);
        var list = await LoadPendingAsync();
        list.RemoveAll(p => p.LocalId == draft.LocalId);
        list.Insert(0, pending);
        await storage.SetAsync(StorageKeys.PendingOrders, list);
        await DiscardLocalAsync(draft.LocalId);
        Changed?.Invoke();
        return pending;
    }

    /// <summary>Orders still waiting in the outbox (delivered ones drop off; the server list shows them).</summary>
    public async Task<IReadOnlyList<PendingOrder>> GetPendingAsync()
    {
        var list = await LoadPendingAsync();
        var queued = (await outbox.GetItemsAsync()).Select(i => i.Id).ToHashSet();
        if (list.RemoveAll(p => !queued.Contains(p.OutboxId)) > 0)
            await storage.SetAsync(StorageKeys.PendingOrders, list);
        return list;
    }

    /// <summary>True when a queued order can safely go back into the editor: it was never sent, or the server
    /// rejected it. One that was tried may already exist on the server (only the reply was lost) — resending
    /// it under a new idempotency key would duplicate it.</summary>
    public static bool CanReopen(OutboxItem item) => item.Status == OutboxStatus.NeedsAttention || item.Attempts == 0;

    /// <summary>Takes a queued order back into the editor (its request is withdrawn). Returns null if that
    /// isn't safe any more (see <see cref="CanReopen"/>) — it will show in the server list once synced.</summary>
    public async Task<OrderDraft?> ReopenPendingAsync(Guid localId)
    {
        var pending = (await GetPendingAsync()).FirstOrDefault(p => p.LocalId == localId);
        if (pending is null) return null;
        var item = (await outbox.GetItemsAsync()).FirstOrDefault(i => i.Id == pending.OutboxId);
        if (item is null || !CanReopen(item)) return null;

        await outbox.DiscardAsync(pending.OutboxId);
        var list = await LoadPendingAsync();
        list.RemoveAll(p => p.LocalId == localId);
        await storage.SetAsync(StorageKeys.PendingOrders, list);
        await SaveLocalAsync(pending.Draft);
        return pending.Draft;
    }

    /// <summary>Cancels a Draft or Submitted order on the server (queued like everything else).</summary>
    public async Task CancelAsync(int serverId, string pharmacyName)
    {
        await outbox.EnqueueAsync(new OutboxRequest(
            Kind: CancelKind,
            Title: $"Cancel order #{serverId} · {pharmacyName}",
            Method: "POST",
            Url: $"api/v1/Orders/{serverId}/cancel"));
        Changed?.Invoke();
    }

    /// <summary>Server ids with a cancel still waiting to be sent.</summary>
    public async Task<IReadOnlySet<int>> GetPendingCancelsAsync() =>
        (await outbox.GetItemsAsync())
            .Where(i => i.Kind == CancelKind)
            .Select(i => int.TryParse(i.Url.Split('/')[^2], out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();

    /// <summary>Forget the in-memory copies (sign-out; storage is wiped separately).</summary>
    public void Reset()
    {
        _drafts = null;
        _pending = null;
        Changed?.Invoke();
    }

    public static string OrderRef(Guid localId) => $"order:{localId}";

    private async Task<bool> VisitResolvableAsync(Guid visitLocalId)
    {
        var reference = $"visit:{visitLocalId}";
        if (await outboxStore.GetRefAsync(reference) is not null) return true;               // check-in delivered
        return (await outbox.GetItemsAsync()).Any(i => i.ProducesRef == reference);           // still queued (sent first)
    }

    private async Task<List<OrderDraft>> LoadDraftsAsync() =>
        _drafts ??= await storage.GetAsync<List<OrderDraft>>(StorageKeys.OrderDrafts) ?? [];

    private async Task<List<PendingOrder>> LoadPendingAsync() =>
        _pending ??= await storage.GetAsync<List<PendingOrder>>(StorageKeys.PendingOrders) ?? [];
}

using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Collections;

/// <summary>Sends collections from the field (plan phase 8), always through the outbox:
///
///   POST api/v1/Collections                                 → ref collection:{localId}
///   POST api/v1/Collections/{ref:collection:…}/attachments  one per photo, each waiting for the collection
///
/// so a collection recorded without signal — photos included — reaches the server intact later. Until it
/// does, it counts towards the rep's financial custody on the phone.</summary>
public sealed class CollectionEntryManager(KeyValueStore storage, Outbox outbox, TimeProvider time)
{
    public const string OutboxKind = "Collection";
    public const string PhotoKind = "CollectionPhoto";
    public const string ReconciliationKind = "CashCount";

    private List<PendingCollection>? _pending;

    public event Action? Changed;

    public async Task<PendingCollection> SendAsync(CollectionDraft draft)
    {
        if (CollectionRules.Validate(draft, null) is { Count: > 0 } problems)
            throw new InvalidOperationException(problems[0]);

        var now = time.GetUtcNow().UtcDateTime;
        var reference = $"collection:{draft.LocalId}";
        var split = draft.Allocations.Where(a => a.Amount > 0).ToList();
        var collection = await outbox.EnqueueAsync(new OutboxRequest(
            Kind: OutboxKind,
            Title: $"Collection · {draft.PharmacyName} · EGP {draft.Amount:N0}",
            Method: "POST",
            Url: "api/v1/Collections",
            Body: new
            {
                pharmacyId = draft.PharmacyId,
                amount = draft.Amount,
                collectionDateUtc = now,
                paymentMethod = draft.Method,
                referenceNumber = Blank(draft.ReferenceNumber),
                notes = Blank(draft.Notes),
                allocations = split.Select(a => new { saleId = a.SaleId, amount = a.Amount })
            },
            ProducesRef: reference));

        var photoIds = new List<Guid>();
        foreach (var (photo, index) in draft.Photos.Select((p, i) => (p, i)))
        {
            var item = await outbox.EnqueueAsync(new OutboxRequest(
                Kind: PhotoKind,
                Title: $"{photo.Label} · {draft.PharmacyName}",
                Method: "POST",
                Url: $"api/v1/Collections/{OutboxRefs.Placeholder(reference)}/attachments",
                DependsOn: collection.Id,
                File: new OutboxFile($"{Slug(photo.Label)}-{index + 1}.jpg", photo.ContentType, photo.Base64)));
            photoIds.Add(item.Id);
        }

        var pending = new PendingCollection(draft.LocalId, collection.Id, photoIds, draft.PharmacyName, draft.Amount,
            draft.Method, Blank(draft.ReferenceNumber), now);
        var list = await LoadAsync();
        list.Insert(0, pending);
        await storage.SetAsync(StorageKeys.PendingCollections, list);
        Changed?.Invoke();
        return pending;
    }

    /// <summary>Collections whose request hasn't reached the server yet (photos may still be uploading after).</summary>
    public async Task<IReadOnlyList<PendingCollection>> GetPendingAsync()
    {
        var list = await LoadAsync();
        var queued = (await outbox.GetItemsAsync()).Select(i => i.Id).ToHashSet();
        if (list.RemoveAll(p => !queued.Contains(p.CollectionOutboxId)) > 0)
            await storage.SetAsync(StorageKeys.PendingCollections, list);
        return list;
    }

    /// <summary>Cash held on top of what the server knows about: collected here, not yet synced.</summary>
    public async Task<decimal> GetUnsyncedTotalAsync() => (await GetPendingAsync()).Sum(p => p.Amount);

    /// <summary>Photos still waiting to upload (their collection may already be on the server).</summary>
    public async Task<int> GetPendingPhotoCountAsync() =>
        (await outbox.GetItemsAsync()).Count(i => i.Kind == PhotoKind);

    /// <summary>The rep's cash count (financial reconciliation). Queued after any unsynced collections, so the
    /// server compares the count with a balance that already includes them.</summary>
    public async Task RequestCashCountAsync(decimal countedBalance, string? reason)
    {
        if (countedBalance < 0) throw new InvalidOperationException("The counted amount can't be negative.");
        await outbox.EnqueueAsync(new OutboxRequest(
            Kind: ReconciliationKind,
            Title: $"Cash count · EGP {countedBalance:N0}",
            Method: "POST",
            Url: "api/v1/Collections/mine/reconciliations",
            Body: new { countedBalance, reason = Blank(reason) }));
        Changed?.Invoke();
    }

    public async Task<bool> HasPendingCashCountAsync() =>
        (await outbox.GetItemsAsync()).Any(i => i.Kind == ReconciliationKind);

    public void Reset()
    {
        _pending = null;
        Changed?.Invoke();
    }

    private async Task<List<PendingCollection>> LoadAsync() =>
        _pending ??= await storage.GetAsync<List<PendingCollection>>(StorageKeys.PendingCollections) ?? [];

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Slug(string label) => label.ToLowerInvariant().Replace(' ', '-');
}

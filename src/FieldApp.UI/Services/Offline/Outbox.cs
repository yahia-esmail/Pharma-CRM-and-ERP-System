using System.Text.Json;
using PharmaERP.FieldApp.UI.Services.Api;

namespace PharmaERP.FieldApp.UI.Services.Offline;

public sealed record OutboxSummary(int Pending, int NeedsAttention)
{
    public int Total => Pending + NeedsAttention;
}

/// <summary>The only way features write to the API (plan 4.3): enqueue, and let
/// <see cref="OutboxProcessor"/> deliver. Also backs the Pending sync screen (list / retry / discard).</summary>
public sealed class Outbox(IOutboxStore store, TimeProvider time)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private long _lastSequence;

    /// <summary>Raised whenever the queue changes (enqueue, delivery, failure, discard) — drives the
    /// sync indicator.</summary>
    public event Action? Changed;

    /// <summary>Raised when there is new work, so the sync loop can wake up immediately.</summary>
    public event Action? WorkQueued;

    public async Task<OutboxItem> EnqueueAsync(OutboxRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var now = time.GetUtcNow().UtcDateTime;
            if (_lastSequence == 0)
                _lastSequence = (await store.GetAllAsync()).Select(i => i.Sequence).DefaultIfEmpty(0).Max();
            // Ticks keep order stable across app restarts; +1 keeps it strictly increasing within one tick.
            _lastSequence = Math.Max(_lastSequence + 1, now.Ticks);

            var item = new OutboxItem
            {
                Id = request.Id ?? Guid.NewGuid(),
                Kind = request.Kind,
                Title = request.Title,
                Method = request.Method.ToUpperInvariant(),
                Url = request.Url,
                JsonBody = request.Body is null ? null : JsonSerializer.Serialize(request.Body, ApiJson.Options),
                DependsOn = request.DependsOn,
                ProducesRef = request.ProducesRef,
                CreatedAtUtc = now,
                Sequence = _lastSequence,
                Status = OutboxStatus.Pending
            };
            await store.SaveAsync(item);
            Changed?.Invoke();
            WorkQueued?.Invoke();
            return item;
        }
        finally { _lock.Release(); }
    }

    public Task<IReadOnlyList<OutboxItem>> GetItemsAsync() => store.GetAllAsync();

    public async Task<OutboxSummary> GetSummaryAsync()
    {
        var items = await store.GetAllAsync();
        return new OutboxSummary(
            items.Count(i => i.Status == OutboxStatus.Pending),
            items.Count(i => i.Status == OutboxStatus.NeedsAttention));
    }

    /// <summary>Puts a rejected item back in the queue — for when the cause was fixed server-side
    /// (e.g. the manager raised the pharmacy's credit limit).</summary>
    public async Task RetryAsync(Guid id)
    {
        var item = (await store.GetAllAsync()).FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        item.Status = OutboxStatus.Pending;
        item.NextAttemptAtUtc = null;
        await store.SaveAsync(item);
        Changed?.Invoke();
        WorkQueued?.Invoke();
    }

    /// <summary>Drops an item and everything that depends on it (lines of a discarded order can never
    /// be delivered). Returns the number of items removed.</summary>
    public async Task<int> DiscardAsync(Guid id)
    {
        var items = await store.GetAllAsync();
        var doomed = new HashSet<Guid> { id };
        bool grew;
        do
        {
            grew = false;
            foreach (var item in items)
                if (item.DependsOn is { } parent && doomed.Contains(parent) && doomed.Add(item.Id))
                    grew = true;
        } while (grew);

        foreach (var itemId in doomed.Where(d => items.Any(i => i.Id == d)))
            await store.DeleteAsync(itemId);
        Changed?.Invoke();
        return doomed.Count(d => items.Any(i => i.Id == d));
    }

    public void RequestSync() => WorkQueued?.Invoke();

    internal void NotifyChanged() => Changed?.Invoke();
}

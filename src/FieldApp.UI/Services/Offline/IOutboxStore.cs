using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Offline;

/// <summary>Persistence for the outbox, behind an interface so the processor can be unit-tested
/// without a browser.</summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxItem>> GetAllAsync();
    Task SaveAsync(OutboxItem item);
    Task DeleteAsync(Guid id);
    Task<int?> GetRefAsync(string reference);
    Task SaveRefAsync(string reference, int serverId);
}

public sealed class IndexedDbOutboxStore(LocalDb db) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxItem>> GetAllAsync() =>
        (await db.GetAllAsync<OutboxItem>(LocalDb.OutboxStore)).OrderBy(i => i.Sequence).ToList();

    public Task SaveAsync(OutboxItem item) => db.PutAsync(LocalDb.OutboxStore, item.Id.ToString(), item);

    public Task DeleteAsync(Guid id) => db.RemoveAsync(LocalDb.OutboxStore, id.ToString());

    public async Task<int?> GetRefAsync(string reference) => await db.GetAsync<int?>(LocalDb.RefsStore, reference);

    public Task SaveRefAsync(string reference, int serverId) => db.PutAsync(LocalDb.RefsStore, reference, serverId);
}

using System.Text.Json;
using Microsoft.JSInterop;
using PharmaERP.FieldApp.UI.Services.Api;

namespace PharmaERP.FieldApp.UI.Services.Storage;

/// <summary>Thin .NET wrapper over wwwroot/js/db.js. Values are serialized here (not in JS) so records,
/// enums and DateOnly round-trip exactly as they do over the API.</summary>
public sealed class LocalDb(IJSRuntime js) : IAsyncDisposable
{
    public const string KvStore = "kv";
    public const string OutboxStore = "outbox";
    public const string RefsStore = "refs";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./_content/PharmaERP.FieldApp.UI/js/db.js").AsTask());

    public async Task<T?> GetAsync<T>(string store, string key) =>
        Deserialize<T>(await (await _module.Value).InvokeAsync<string?>("get", store, key));

    public async Task PutAsync<T>(string store, string key, T value) =>
        await (await _module.Value).InvokeVoidAsync("put", store, key, JsonSerializer.Serialize(value, ApiJson.Options));

    public async Task RemoveAsync(string store, string key) =>
        await (await _module.Value).InvokeVoidAsync("remove", store, key);

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string store)
    {
        var values = await (await _module.Value).InvokeAsync<string[]>("getAll", store);
        return values.Select(Deserialize<T>).OfType<T>().ToList();
    }

    public async Task<int> CountAsync(string store) => await (await _module.Value).InvokeAsync<int>("count", store);

    public async Task ClearAsync(string store) => await (await _module.Value).InvokeVoidAsync("clear", store);

    public async Task ClearAllAsync() => await (await _module.Value).InvokeVoidAsync("clearAll");

    public async Task<bool> RequestPersistenceAsync() =>
        await (await _module.Value).InvokeAsync<bool>("requestPersistence");

    private static T? Deserialize<T>(string? json)
    {
        if (json is null) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, ApiJson.Options);
        }
        catch (JsonException)
        {
            // Shape changed between app versions — treat the stale entry as missing.
            return default;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated) await (await _module.Value).DisposeAsync();
    }
}

/// <summary>Session and cached snapshots ("kv" store).</summary>
public sealed class KeyValueStore(LocalDb db)
{
    public Task<T?> GetAsync<T>(string key) => db.GetAsync<T>(LocalDb.KvStore, key);

    public Task SetAsync<T>(string key, T value) => db.PutAsync(LocalDb.KvStore, key, value);

    public Task RemoveAsync(string key) => db.RemoveAsync(LocalDb.KvStore, key);

    /// <summary>Wipes every store (session, caches, outbox) — used on sign-out.</summary>
    public Task ClearAsync() => db.ClearAllAsync();

    public Task<bool> RequestPersistenceAsync() => db.RequestPersistenceAsync();
}

/// <summary>Keys used in the "kv" store, kept in one place so logout can reason about what it clears.</summary>
public static class StorageKeys
{
    public const string Session = "session";
    public const string Profile = "profile";
    public const string Dashboard = "cache:dashboard";

    public static string MasterData(string dataset) => $"master:{dataset}";

    public static string PharmacyAccount(int pharmacyId) => $"cache:pharmacyAccount:{pharmacyId}";

    public const string Orders = "cache:orders";
    public static string OrderDetail(int orderId) => $"cache:order:{orderId}";
    public const string OrderDrafts = "orders:drafts";
    public const string PendingOrders = "orders:pending";

    public const string FinancialCustody = "cache:financialCustody";
    public static string CustodyLedger(int productId) => $"cache:custodyLedger:{productId}";
    public const string PendingCollections = "collections:pending";

    public const string Returns = "cache:returns";
    public const string Expenses = "cache:expenses";
    public const string Warehouses = "cache:warehouses";
    public const string PendingReturns = "returns:pending";
    public const string PendingExpenses = "expenses:pending";

    public const string Notifications = "cache:notifications";
}

using Microsoft.Extensions.Logging;
using PharmaERP.Application.Orders;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Orders;

/// <summary>The rep's orders as the server knows them (wireframe 4), cached so the list and the order details
/// still open without signal.</summary>
public sealed class OrdersStore(OrdersApi api, KeyValueStore storage, TimeProvider time, ILogger<OrdersStore> logger)
{
    public Task<Cached<IReadOnlyList<OrderListItemDto>>?> GetCachedAsync() =>
        storage.GetAsync<Cached<IReadOnlyList<OrderListItemDto>>>(StorageKeys.Orders);

    /// <summary>Re-downloads the list. Returns null when the server couldn't be reached (the cache is kept).</summary>
    public async Task<Cached<IReadOnlyList<OrderListItemDto>>?> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var fresh = new Cached<IReadOnlyList<OrderListItemDto>>(await api.GetMineAsync(ct: ct), time.GetUtcNow().UtcDateTime);
            await storage.SetAsync(StorageKeys.Orders, fresh);
            return fresh;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation("Orders not refreshed: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>An order with its lines: from the server when reachable, else the copy saved last time.</summary>
    public async Task<OrderDetailDto?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        try
        {
            if (await api.GetByIdAsync(id, ct) is { } detail)
            {
                await storage.SetAsync(StorageKeys.OrderDetail(id), detail);
                return detail;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation("Order {Id} not refreshed: {Error}", id, ex.Message);
        }
        return await storage.GetAsync<OrderDetailDto>(StorageKeys.OrderDetail(id));
    }
}

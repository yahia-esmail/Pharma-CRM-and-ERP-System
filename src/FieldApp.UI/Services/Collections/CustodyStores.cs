using Microsoft.Extensions.Logging;
using PharmaERP.Application.Custody;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Collections;

/// <summary>Financial custody (wireframe 5): the server's totals, recent collections and cash counts, cached so
/// the tab opens without signal.</summary>
public sealed class FinancialCustodyStore(CollectionsApi api, KeyValueStore storage, TimeProvider time,
    ILogger<FinancialCustodyStore> logger)
{
    public Task<Cached<FinancialCustodySnapshot>?> GetCachedAsync() =>
        storage.GetAsync<Cached<FinancialCustodySnapshot>>(StorageKeys.FinancialCustody);

    /// <summary>Null when the server couldn't be reached (the cached copy is kept).</summary>
    public async Task<Cached<FinancialCustodySnapshot>?> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var custody = await api.GetMyCustodyAsync(ct);
            if (custody is null) return null;
            var snapshot = new Cached<FinancialCustodySnapshot>(
                new FinancialCustodySnapshot(custody, await api.GetMineAsync(ct: ct), await api.GetMyReconciliationsAsync(ct)),
                time.GetUtcNow().UtcDateTime);
            await storage.SetAsync(StorageKeys.FinancialCustody, snapshot);
            return snapshot;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation("Financial custody not refreshed: {Error}", ex.Message);
            return null;
        }
    }
}

/// <summary>One product's custody movements (received, sold, returned…), cached per product.</summary>
public sealed class CustodyLedgerStore(CustodyApi api, KeyValueStore storage, TimeProvider time, ILogger<CustodyLedgerStore> logger)
{
    public async Task<Cached<IReadOnlyList<CustodyTransactionDto>>?> GetAsync(int productId, CancellationToken ct = default)
    {
        try
        {
            var fresh = new Cached<IReadOnlyList<CustodyTransactionDto>>(await api.GetMyLedgerAsync(productId, ct), time.GetUtcNow().UtcDateTime);
            await storage.SetAsync(StorageKeys.CustodyLedger(productId), fresh);
            return fresh;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation("Custody ledger {Product} not refreshed: {Error}", productId, ex.Message);
            return await storage.GetAsync<Cached<IReadOnlyList<CustodyTransactionDto>>>(StorageKeys.CustodyLedger(productId));
        }
    }
}

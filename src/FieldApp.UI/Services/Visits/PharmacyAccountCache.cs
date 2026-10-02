using Microsoft.Extensions.Logging;
using PharmaERP.Application.Pharmacies;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Visits;

/// <summary>A pharmacy's account as the visit screen shows it (wireframe 8): the ledger plus the credit
/// terms, which only the detail endpoint carries.</summary>
public sealed record PharmacyAccount(PharmacyLedgerDto Ledger, decimal? CreditLimit, int? PaymentTermDays)
{
    /// <summary>Headroom left under the credit limit; null when there's no limit (0 means unlimited).</summary>
    public decimal? AvailableCredit => CreditLimit is > 0 and var limit ? limit - Ledger.OutstandingBalance : null;

    public bool OverCreditLimit => AvailableCredit < 0;

    public int OverdueInvoices => Ledger.Lines.Count(l => l.DaysOverdue > 0);
}

/// <summary>Account snapshots of the pharmacies visited (plan 8.1 "ledgers"): fetched when the visit screen
/// opens, and the last copy is kept so the snapshot still shows without signal.</summary>
public sealed class PharmacyAccountCache(PharmaciesApi api, KeyValueStore storage, TimeProvider time,
    ILogger<PharmacyAccountCache> logger)
{
    public Task<Cached<PharmacyAccount>?> GetCachedAsync(int pharmacyId) =>
        storage.GetAsync<Cached<PharmacyAccount>>(StorageKeys.PharmacyAccount(pharmacyId));

    /// <summary>Fresh from the server when reachable, otherwise the last saved copy (or null if never seen).</summary>
    public async Task<Cached<PharmacyAccount>?> GetAsync(int pharmacyId, CancellationToken ct = default)
    {
        try
        {
            var ledger = await api.GetLedgerAsync(pharmacyId, ct);
            if (ledger is null) return await GetCachedAsync(pharmacyId);

            PharmacyDetailDto? detail = null;
            try
            {
                detail = await api.GetByIdAsync(pharmacyId, ct);
            }
            catch (HttpRequestException ex)
            {
                // The ledger alone is still worth showing; keep the credit terms we knew before.
                logger.LogInformation("Pharmacy {Id} detail not refreshed: {Error}", pharmacyId, ex.StatusCode);
            }

            var previous = detail is null ? await GetCachedAsync(pharmacyId) : null;
            var fresh = new Cached<PharmacyAccount>(
                new PharmacyAccount(ledger,
                    detail?.CreditLimit ?? previous?.Data.CreditLimit,
                    detail?.PaymentTermDays ?? previous?.Data.PaymentTermDays),
                time.GetUtcNow().UtcDateTime);
            await storage.SetAsync(StorageKeys.PharmacyAccount(pharmacyId), fresh);
            return fresh;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Offline, timed out, or the server failed: fall back to the last snapshot.
            logger.LogInformation("Pharmacy {Id} account not refreshed: {Error}", pharmacyId, ex.Message);
            return await GetCachedAsync(pharmacyId);
        }
    }
}

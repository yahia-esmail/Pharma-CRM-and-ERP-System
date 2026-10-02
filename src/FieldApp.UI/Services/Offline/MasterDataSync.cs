using Microsoft.Extensions.Logging;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Doctors;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.Products;
using PharmaERP.Application.VisitPlans;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Offline;

/// <summary>A cached API snapshot with the time it was taken, so screens can say "as of 09:40".</summary>
public sealed record Cached<T>(T Data, DateTime SavedAtUtc);

public sealed record DatasetStatus(string Name, DateTime? SavedAtUtc, int? Count, string? LastError);

/// <summary>Keeps the reference data a rep needs offline (plan 8.1): today's plan, custody balances,
/// and the doctors / pharmacies / products they work with. Each dataset refreshes when older than its
/// own max age, so opening the app repeatedly doesn't re-download the catalogue every time.</summary>
public sealed class MasterDataSync(
    KeyValueStore storage,
    ProductsApi productsApi,
    DoctorsApi doctorsApi,
    PharmaciesApi pharmaciesApi,
    VisitPlansApi visitPlansApi,
    CustodyApi custodyApi,
    TimeProvider time,
    ILogger<MasterDataSync> logger)
{
    public const string TodayPlan = "todayPlan";
    public const string Custody = "custody";
    public const string Doctors = "doctors";
    public const string Pharmacies = "pharmacies";
    public const string Products = "products";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, string> _errors = [];

    private sealed record Dataset(string Name, TimeSpan MaxAge, Func<CancellationToken, Task<object>> Fetch, Func<Task<(DateTime?, int?)>> Describe);

    private IEnumerable<Dataset> Datasets =>
    [
        Define(TodayPlan, TimeSpan.FromMinutes(15), visitPlansApi.GetTodayAsync),
        Define(Custody, TimeSpan.FromMinutes(15), custodyApi.GetMyBalancesAsync),
        Define(Doctors, TimeSpan.FromHours(12), doctorsApi.GetAllAsync),
        Define(Pharmacies, TimeSpan.FromHours(12), pharmaciesApi.GetAllAsync),
        Define(Products, TimeSpan.FromHours(12), productsApi.GetAllAsync)
    ];

    public event Action? Synced;

    public async Task<Cached<IReadOnlyList<VisitPlanItemDto>>?> GetTodayPlanAsync() => await Get<VisitPlanItemDto>(TodayPlan);
    public async Task<Cached<IReadOnlyList<RepStockCustodyBalanceDto>>?> GetCustodyAsync() => await Get<RepStockCustodyBalanceDto>(Custody);
    public async Task<Cached<IReadOnlyList<DoctorListItemDto>>?> GetDoctorsAsync() => await Get<DoctorListItemDto>(Doctors);
    public async Task<Cached<IReadOnlyList<PharmacyListItemDto>>?> GetPharmaciesAsync() => await Get<PharmacyListItemDto>(Pharmacies);
    public async Task<Cached<IReadOnlyList<ProductListItemDto>>?> GetProductsAsync() => await Get<ProductListItemDto>(Products);

    /// <summary>Refreshes stale datasets (or all of them with <paramref name="force"/>). Failures are
    /// per dataset: an unreachable endpoint keeps its previous snapshot and doesn't block the others.</summary>
    public async Task SyncAsync(bool force = false, CancellationToken ct = default) =>
        await SyncCoreAsync(force, only: null, wait: false, ct);

    /// <summary>Re-downloads one dataset now (pull-to-refresh on a screen). Waits for a running sync
    /// instead of skipping, so the caller sees fresh data when it returns.</summary>
    public async Task RefreshAsync(string dataset, CancellationToken ct = default) =>
        await SyncCoreAsync(force: true, only: dataset, wait: true, ct);

    /// <summary>Error of the last attempt to refresh <paramref name="dataset"/>, if it failed.</summary>
    public string? LastError(string dataset) => _errors.GetValueOrDefault(dataset);

    private async Task SyncCoreAsync(bool force, string? only, bool wait, CancellationToken ct)
    {
        if (wait) await _lock.WaitAsync(ct);
        else if (!await _lock.WaitAsync(0, ct)) return;   // a sync is already running
        try
        {
            var now = time.GetUtcNow().UtcDateTime;
            foreach (var dataset in Datasets.Where(d => only is null || d.Name == only))
            {
                var (savedAt, _) = await dataset.Describe();
                if (!force && savedAt is { } s && now - s < dataset.MaxAge) continue;

                try
                {
                    await storage.SetAsync(StorageKeys.MasterData(dataset.Name), await dataset.Fetch(ct));
                    _errors.Remove(dataset.Name);
                }
                catch (HttpRequestException ex)
                {
                    _errors[dataset.Name] = ex.StatusCode is { } code ? $"HTTP {(int)code}" : "unreachable";
                    logger.LogInformation("Master data '{Dataset}' not refreshed: {Error}", dataset.Name, _errors[dataset.Name]);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
        Synced?.Invoke();
    }

    /// <summary>Writes a customer's newly captured location into the offline copies (customer list and
    /// today's plan) so nearby ranking, the map and the geofence use it before the next download.</summary>
    public async Task SetCustomerLocationAsync(Plan.StopKind kind, int customerId, double latitude, double longitude)
    {
        await _lock.WaitAsync();
        try
        {
            if (kind == Plan.StopKind.Doctor && await Get<DoctorListItemDto>(Doctors) is { } doctors)
                await storage.SetAsync(StorageKeys.MasterData(Doctors), doctors with
                {
                    Data = doctors.Data.Select(d => d.Id == customerId ? d with { Latitude = latitude, Longitude = longitude } : d).ToList()
                });
            if (kind == Plan.StopKind.Pharmacy && await Get<PharmacyListItemDto>(Pharmacies) is { } pharmacies)
                await storage.SetAsync(StorageKeys.MasterData(Pharmacies), pharmacies with
                {
                    Data = pharmacies.Data.Select(p => p.Id == customerId ? p with { Latitude = latitude, Longitude = longitude } : p).ToList()
                });
            if (await Get<VisitPlanItemDto>(TodayPlan) is { } plan)
                await storage.SetAsync(StorageKeys.MasterData(TodayPlan), plan with
                {
                    Data = plan.Data.Select(i =>
                        (kind == Plan.StopKind.Doctor ? i.DoctorId : i.PharmacyId) == customerId
                            ? i with { Latitude = latitude, Longitude = longitude }
                            : i).ToList()
                });
        }
        finally
        {
            _lock.Release();
        }
        Synced?.Invoke();
    }

    public async Task<IReadOnlyList<DatasetStatus>> GetStatusAsync()
    {
        var result = new List<DatasetStatus>();
        foreach (var dataset in Datasets)
        {
            var (savedAt, count) = await dataset.Describe();
            result.Add(new DatasetStatus(dataset.Name, savedAt, count, _errors.GetValueOrDefault(dataset.Name)));
        }
        return result;
    }

    private Task<Cached<IReadOnlyList<T>>?> Get<T>(string name) =>
        storage.GetAsync<Cached<IReadOnlyList<T>>>(StorageKeys.MasterData(name));

    private Dataset Define<T>(string name, TimeSpan maxAge, Func<CancellationToken, Task<IReadOnlyList<T>>> fetch) =>
        new(name, maxAge,
            async ct => new Cached<IReadOnlyList<T>>(await fetch(ct), time.GetUtcNow().UtcDateTime),
            async () => await Get<T>(name) is { } cached ? (cached.SavedAtUtc, cached.Data.Count) : (null, null));
}

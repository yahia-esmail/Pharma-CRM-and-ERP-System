using System.Net.Http.Json;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Dashboard;
using PharmaERP.Application.Doctors;
using PharmaERP.Application.Expenses;
using PharmaERP.Application.Returns;
using PharmaERP.Application.Warehouses;
using PharmaERP.Application.Orders;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.Products;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Shared.Common;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Services.Api;

// One typed client per API controller (plan 4.2). Each is registered with AuthHeaderHandler in
// FieldAppServiceCollectionExtensions. Clients for the remaining controllers are added with the phase
// that first needs them.

public sealed class UsersApi(HttpClient http)
{
    public Task<UserProfileDto?> GetMeAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<UserProfileDto>("api/v1/Users/me", ApiJson.Options, ct);
}

public sealed class DashboardApi(HttpClient http)
{
    public Task<RepresentativeDashboardDto?> GetMineAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<RepresentativeDashboardDto>("api/v1/Dashboard/mine", ApiJson.Options, ct);
}

public sealed class ProductsApi(HttpClient http)
{
    public Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        Paging.GetAllAsync<ProductListItemDto>(http, "api/v1/Products", ct);
}

public sealed class DoctorsApi(HttpClient http)
{
    // The API scopes the list to what the signed-in rep may see (their territory / assignments).
    public Task<IReadOnlyList<DoctorListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        Paging.GetAllAsync<DoctorListItemDto>(http, "api/v1/Doctors", ct);
}

public sealed class PharmaciesApi(HttpClient http)
{
    public Task<IReadOnlyList<PharmacyListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        Paging.GetAllAsync<PharmacyListItemDto>(http, "api/v1/Pharmacies", ct);

    public Task<PharmacyDetailDto?> GetByIdAsync(int pharmacyId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<PharmacyDetailDto>($"api/v1/Pharmacies/{pharmacyId}", ApiJson.Options, ct);

    public Task<PharmacyLedgerDto?> GetLedgerAsync(int pharmacyId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<PharmacyLedgerDto>($"api/v1/Pharmacies/{pharmacyId}/ledger", ApiJson.Options, ct);
}

public sealed class OrdersApi(HttpClient http)
{
    // The API scopes the list to the signed-in rep's own orders; newest first.
    public async Task<IReadOnlyList<OrderListItemDto>> GetMineAsync(int take = 100, CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<PagedResult<OrderListItemDto>>($"api/v1/Orders?pageNumber=1&pageSize={take}", ApiJson.Options, ct))?.Items ?? [];

    public Task<OrderDetailDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<OrderDetailDto>($"api/v1/Orders/{id}", ApiJson.Options, ct);
}

public sealed class VisitPlansApi(HttpClient http)
{
    public async Task<IReadOnlyList<VisitPlanItemDto>> GetTodayAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<VisitPlanItemDto>>("api/v1/VisitPlans/today", ApiJson.Options, ct) ?? [];
}

public sealed class CustodyApi(HttpClient http)
{
    public async Task<IReadOnlyList<RepStockCustodyBalanceDto>> GetMyBalancesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RepStockCustodyBalanceDto>>("api/v1/Custody/mine/balances", ApiJson.Options, ct) ?? [];

    public async Task<IReadOnlyList<CustodyTransactionDto>> GetMyLedgerAsync(int productId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CustodyTransactionDto>>($"api/v1/Custody/mine/ledger?productId={productId}", ApiJson.Options, ct) ?? [];
}

public sealed class ReturnsApi(HttpClient http)
{
    public async Task<IReadOnlyList<ReturnTransactionDto>> GetMineAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ReturnTransactionDto>>("api/v1/Returns/mine", ApiJson.Options, ct) ?? [];
}

public sealed class ExpensesApi(HttpClient http)
{
    public async Task<IReadOnlyList<ExpenseDto>> GetMineAsync(int take = 50, CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<PagedResult<ExpenseDto>>($"api/v1/Expenses/mine?pageNumber=1&pageSize={take}", ApiJson.Options, ct))?.Items ?? [];
}

public sealed class WarehousesApi(HttpClient http)
{
    public async Task<IReadOnlyList<WarehouseListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<WarehouseListItemDto>>("api/v1/Warehouses", ApiJson.Options, ct) ?? [];
}

public sealed class CollectionsApi(HttpClient http)
{
    public async Task<IReadOnlyList<CollectionDto>> GetMineAsync(int take = 30, CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<PagedResult<CollectionDto>>($"api/v1/Collections/mine?pageNumber=1&pageSize={take}", ApiJson.Options, ct))?.Items ?? [];

    public Task<RepFinancialCustodyDto?> GetMyCustodyAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<RepFinancialCustodyDto>("api/v1/Collections/mine/custody", ApiJson.Options, ct);

    public async Task<IReadOnlyList<FinancialReconciliationDto>> GetMyReconciliationsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<FinancialReconciliationDto>>("api/v1/Collections/mine/reconciliations", ApiJson.Options, ct) ?? [];
}

internal static class Paging
{
    // PagedRequest caps pageSize at 200 server-side.
    private const int PageSize = 200;
    private const int MaxPages = 50;

    /// <summary>Reads every page of a PagedResult endpoint. TODO(plan 6.2 #18): switch to
    /// <c>updatedSince</c> delta sync once the API supports it, instead of re-downloading everything.</summary>
    public static async Task<IReadOnlyList<T>> GetAllAsync<T>(HttpClient http, string url, CancellationToken ct)
    {
        var all = new List<T>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var result = await http.GetFromJsonAsync<PagedResult<T>>(
                $"{url}?pageNumber={page}&pageSize={PageSize}", ApiJson.Options, ct);
            if (result is null) break;
            all.AddRange(result.Items);
            if (result.Items.Count < PageSize || page >= result.TotalPages) break;
        }
        return all;
    }
}

public sealed class NotificationsApi(HttpClient http)
{
    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/v1/Notifications/unread-count", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(ApiJson.Options, ct);
    }
}

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaERP.FieldApp.UI.Services;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Auth;
using PharmaERP.FieldApp.UI.Services.Device;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Orders;
using PharmaERP.FieldApp.UI.Services.Collections;
using PharmaERP.FieldApp.UI.Services.Requests;
using PharmaERP.FieldApp.UI.Services.Notifications;
using PharmaERP.FieldApp.UI.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Services.Visits;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI;

public static class FieldAppServiceCollectionExtensions
{
    /// <summary>Registers everything the field app needs, so each host (WASM PWA today, MAUI Hybrid
    /// later) only adds its root components and swaps device implementations if it has native ones.</summary>
    public static IServiceCollection AddFieldApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.SectionName));
        services.Configure<GpsOptions>(configuration.GetSection(GpsOptions.SectionName));
        services.Configure<MapOptions>(configuration.GetSection(MapOptions.SectionName));

        var apiBase = configuration[$"{ApiOptions.SectionName}:BaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBase))
            throw new InvalidOperationException("Api:BaseUrl is missing from wwwroot/appsettings.json.");
        var apiBaseUri = new Uri(apiBase.EndsWith('/') ? apiBase : apiBase + "/");

        // Singletons: IHttpClientFactory resolves handlers in a separate scope, and in WebAssembly a
        // scoped service is effectively app-wide anyway — singletons make the sharing explicit.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LocalDb>();
        services.AddSingleton<KeyValueStore>();
        services.AddSingleton<TokenStore>();
        services.AddSingleton<SessionRefresher>();
        services.AddSingleton<AuthApi>();
        services.AddSingleton<BrowserDevice>();
        services.AddSingleton<INetworkStatus>(sp => sp.GetRequiredService<BrowserDevice>());
        services.AddTransient<AuthHeaderHandler>();

        services.AddHttpClient(HttpClientNames.Anonymous, c => c.BaseAddress = apiBaseUri);

        // No automatic retries (resilience handler) on purpose: reads fall back to the local cache, and
        // writes go through the outbox, which retries with an Idempotency-Key so nothing is duplicated.
        void AddApi<TClient>() where TClient : class =>
            services.AddHttpClient<TClient>(c => c.BaseAddress = apiBaseUri)
                .AddHttpMessageHandler<AuthHeaderHandler>();

        AddApi<UsersApi>();
        AddApi<DashboardApi>();
        AddApi<NotificationsApi>();
        AddApi<ProductsApi>();
        AddApi<DoctorsApi>();
        AddApi<PharmaciesApi>();
        AddApi<VisitPlansApi>();
        AddApi<CustodyApi>();
        AddApi<OrdersApi>();
        AddApi<CollectionsApi>();
        AddApi<ReturnsApi>();
        AddApi<ExpensesApi>();
        AddApi<WarehousesApi>();
        AddApi<ClientErrorsApi>();
        AddApi<PasskeysApi>();

        // Offline-first (plan 8): every write is queued in the outbox and delivered by the sync loop.
        services.AddHttpClient(OutboxProcessor.HttpClientName, c => c.BaseAddress = apiBaseUri)
            .AddHttpMessageHandler<AuthHeaderHandler>();
        services.AddSingleton<IOutboxStore, IndexedDbOutboxStore>();
        services.AddSingleton<Outbox>();
        services.AddSingleton<OutboxProcessor>();
        services.AddSingleton<MasterDataSync>();
        services.AddSingleton<SyncLoop>();

        // GPS (plan 7): browser geolocation behind ILocationService; a MAUI host would swap in a native one.
        services.AddSingleton<ILocationService, BrowserLocationService>();
        services.AddSingleton<ITrackerStorage, KvTrackerStorage>();
        services.AddSingleton<LocationTracker>();
        services.AddSingleton<LocationTrackerRunner>();

        services.AddSingleton<TodayPlanState>();
        services.AddSingleton<VisitSessionManager>();
        services.AddSingleton<VisitTargetResolver>();
        services.AddSingleton<PharmacyAccountCache>();
        services.AddSingleton<OrderEntryManager>();
        services.AddSingleton<OrdersStore>();
        services.AddSingleton<CollectionEntryManager>();
        services.AddSingleton<FinancialCustodyStore>();
        services.AddSingleton<CustodyLedgerStore>();
        services.AddSingleton<ReturnEntryManager>();
        services.AddSingleton<ExpenseEntryManager>();
        services.AddSingleton<RequestsStore>();
        services.AddSingleton<NotificationCenter>();
        services.AddSingleton<PushService>();

        // Errors on the phone reach the server log (plan phase 11), scrubbed of tokens and personal data.
        services.AddSingleton<ILoggerProvider, ClientErrorSink>();
        services.AddSingleton<ClientErrorReporter>();
        services.AddSingleton<PasskeyService>();

        services.AddSingleton<SessionState>();
        services.AddSingleton<AuthService>();

        services.AddAuthorizationCore();
        services.AddSingleton<AuthenticationStateProvider, JwtAuthStateProvider>();

        return services;
    }
}

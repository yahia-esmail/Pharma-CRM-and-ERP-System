using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Dashboard;
using PharmaERP.Application.DistrictDashboard;
using PharmaERP.Application.Doctors;
using PharmaERP.Application.Expenses;
using PharmaERP.Application.Files;
using PharmaERP.Application.Lists;
using PharmaERP.Application.Location;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Orders;
using PharmaERP.Application.Performance;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.Products;
using PharmaERP.Application.Purchasing;
using PharmaERP.Application.Representatives;
using PharmaERP.Application.Returns;
using PharmaERP.Application.Sales;
using PharmaERP.Application.Suppliers;
using PharmaERP.Application.Territories;
using PharmaERP.Application.Traceability;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Application.Visits;
using PharmaERP.Application.Warehouses;

namespace PharmaERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VisitValidationOptions>(configuration.GetSection("VisitValidation"));
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.AddScoped<IVisitValidationService, VisitValidationService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IRepresentativeService, RepresentativeService>();
        services.AddScoped<ITerritoryService, TerritoryService>();
        services.AddScoped<IVisitPlanService, VisitPlanService>();
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<BusinessOptions>(configuration.GetSection(BusinessOptions.SectionName));
        services.AddSingleton<IBusinessCalendar, BusinessCalendar>();
        services.AddScoped<IVisitSessionService, VisitSessionService>();
        services.AddScoped<ILocationProposalService, LocationProposalService>();
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPharmacyService, PharmacyService>();
        services.AddScoped<IPharmacyBalanceCalculator, PharmacyBalanceCalculator>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ICustodyService, CustodyService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRepresentativeDashboardService, RepresentativeDashboardService>();
        services.AddScoped<ITraceService, TraceService>();
        services.AddScoped<IListService, ListService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IDistrictDashboardService, DistrictDashboardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFileAttachmentService, FileAttachmentService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        return services;
    }
}

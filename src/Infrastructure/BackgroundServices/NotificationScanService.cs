using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Warehouses;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Security;

namespace PharmaERP.Infrastructure.BackgroundServices;

/// <summary>Periodic scan for the time-based half of the Notifications trigger catalog (addendum 3.10) —
/// the event-driven half (Order submitted, Collection recorded) is raised directly from OrderService/
/// CollectionService instead. Registered only in Web.Mvc (not Web.Api) so exactly one process runs the
/// scan even though both front ends share the same database.</summary>
public class NotificationScanService(IServiceScopeFactory scopeFactory, IOptions<NotificationOptions> options,
    ILogger<NotificationScanService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification scan failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(options.Value.ScanIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userDirectory = scope.ServiceProvider.GetRequiredService<IUserDirectoryService>();
        var warehouseService = scope.ServiceProvider.GetRequiredService<IWarehouseService>();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
        var settings = options.Value;

        await ScanVisitPlansPendingApprovalAsync(db, notificationService, settings, ct);
        await ScanPlannedVisitsNotLoggedAsync(db, notificationService, ct);
        await ScanLowStockAsync(warehouseService, userDirectory, notificationService, ct);
        await ScanNearExpiryAsync(warehouseService, userDirectory, notificationService, settings, ct);
        await ScanCustodyBalanceAgingAsync(collectionService, db, userDirectory, notificationService, settings, ct);
        await ScanSupplierPaymentsDueAsync(db, userDirectory, notificationService, settings, ct);
    }

    /// <summary>Trigger 1 — a Submitted plan that's sat too long notifies its territory's district manager.</summary>
    private static async Task ScanVisitPlansPendingApprovalAsync(IAppDbContext db, INotificationService notificationService,
        NotificationOptions settings, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddHours(-settings.VisitPlanApprovalThresholdHours);
        var pendingPlans = await db.VisitPlans.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == VisitPlanStatus.Submitted && p.SubmittedAtUtc <= cutoff)
            .Select(p => new { p.Id, p.RepresentativeId, RepresentativeName = p.Representative.FullName })
            .ToListAsync(ct);

        foreach (var plan in pendingPlans)
        {
            var dmUserId = await GetDistrictManagerUserIdForRepresentativeAsync(db, plan.RepresentativeId, ct);
            if (string.IsNullOrEmpty(dmUserId)) continue;

            await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(dmUserId, NotificationTypes.VisitPlanPendingApproval,
                $"Visit plan #{plan.Id} for {plan.RepresentativeName} has been awaiting approval for over {settings.VisitPlanApprovalThresholdHours}h.",
                nameof(VisitPlan), plan.Id, ct);
        }
    }

    /// <summary>Trigger 2 — yesterday's planned doctor visits that were never logged notify the rep and their DM.</summary>
    private static async Task ScanPlannedVisitsNotLoggedAsync(IAppDbContext db, INotificationService notificationService, CancellationToken ct)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var dayStartUtc = yesterday.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = yesterday.ToDateTime(TimeOnly.MaxValue);

        var plannedItems = await db.VisitPlanItems.AsNoTracking()
            .Where(i => !i.IsDeleted && i.PlannedDate == yesterday && i.VisitPlan.Status == VisitPlanStatus.Approved)
            .Select(i => new { i.Id, i.DoctorId, DoctorName = i.Doctor.FullName, i.VisitPlan.RepresentativeId,
                RepresentativeUserId = i.VisitPlan.Representative.ApplicationUserId })
            .ToListAsync(ct);

        foreach (var item in plannedItems)
        {
            var wasLogged = await db.DoctorVisits.AsNoTracking().AnyAsync(v => !v.IsDeleted
                && v.DoctorId == item.DoctorId && v.RepresentativeId == item.RepresentativeId
                && v.VisitDateUtc >= dayStartUtc && v.VisitDateUtc <= dayEndUtc, ct);
            if (wasLogged) continue;

            var message = $"Planned visit to {item.DoctorName} on {yesterday:yyyy-MM-dd} was not logged.";
            if (!string.IsNullOrEmpty(item.RepresentativeUserId))
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(item.RepresentativeUserId,
                    NotificationTypes.PlannedVisitNotLogged, message, nameof(Domain.Entities.VisitPlanItem), item.Id, ct);

            var dmUserId = await GetDistrictManagerUserIdForRepresentativeAsync(db, item.RepresentativeId, ct);
            if (!string.IsNullOrEmpty(dmUserId))
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(dmUserId,
                    NotificationTypes.PlannedVisitNotLogged, message, nameof(Domain.Entities.VisitPlanItem), item.Id, ct);
        }
    }

    /// <summary>Trigger 5 — reuses WarehouseService's existing ReorderLevel-based check, the same one the
    /// Management Dashboard already shows, instead of a second threshold definition.</summary>
    private static async Task ScanLowStockAsync(IWarehouseService warehouseService, IUserDirectoryService userDirectory,
        INotificationService notificationService, CancellationToken ct)
    {
        var lowStock = await warehouseService.GetLowStockAsync(ct);
        if (lowStock.Count == 0) return;

        var warehouseUserIds = await userDirectory.GetUserIdsInRoleAsync(Roles.Warehouse, ct);
        foreach (var product in lowStock)
        {
            foreach (var userId in warehouseUserIds)
            {
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(userId, NotificationTypes.LowStock,
                    $"'{product.ProductName}' is at or below its reorder level ({product.TotalWarehouseQuantity}/{product.ReorderLevel}).",
                    nameof(Product), product.ProductId, ct);
            }
        }
    }

    /// <summary>Trigger 6 — reuses the same near-expiry window already shown on the Management Dashboard.</summary>
    private static async Task ScanNearExpiryAsync(IWarehouseService warehouseService, IUserDirectoryService userDirectory,
        INotificationService notificationService, NotificationOptions settings, CancellationToken ct)
    {
        var nearExpiry = await warehouseService.GetNearExpiryAsync(settings.NearExpiryThresholdDays, ct);
        if (nearExpiry.Count == 0) return;

        var recipientUserIds = (await userDirectory.GetUserIdsInRoleAsync(Roles.Warehouse, ct))
            .Concat(await userDirectory.GetUserIdsInRoleAsync(Roles.SalesManager, ct))
            .Distinct();

        foreach (var batch in nearExpiry)
        {
            foreach (var userId in recipientUserIds)
            {
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(userId, NotificationTypes.NearExpiry,
                    $"'{batch.ProductName}' batch {batch.BatchNumber} expires in {batch.DaysUntilExpiry} day(s) ({batch.ExpiryDate:yyyy-MM-dd}).",
                    nameof(ProductBatch), batch.ProductBatchId, ct);
            }
        }
    }

    /// <summary>Trigger 7 — reuses the same FIFO "oldest unremitted collection" aging already computed for
    /// the custody summary/dashboard.</summary>
    private static async Task ScanCustodyBalanceAgingAsync(ICollectionService collectionService, IAppDbContext db,
        IUserDirectoryService userDirectory, INotificationService notificationService, NotificationOptions settings, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-settings.OverdueCustodyDays);
        var summary = await collectionService.GetFinancialCustodySummaryAsync(null, ct);
        var financeUserIds = await userDirectory.GetUserIdsInRoleAsync(Roles.Finance, ct);

        foreach (var rep in summary)
        {
            if (rep.OutstandingBalance <= 0 || rep.OldestUnremittedCollectionDateUtc is not { } oldest || oldest > cutoff)
                continue;

            var message = $"{rep.RepresentativeName}'s outstanding custody balance ({rep.OutstandingBalance:C}) has an unremitted collection from {oldest:yyyy-MM-dd}.";
            foreach (var userId in financeUserIds)
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(userId, NotificationTypes.CustodyBalanceAging,
                    message, nameof(Representative), rep.RepresentativeId, ct);

            var dmUserId = await GetDistrictManagerUserIdForRepresentativeAsync(db, rep.RepresentativeId, ct);
            if (!string.IsNullOrEmpty(dmUserId))
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(dmUserId, NotificationTypes.CustodyBalanceAging,
                    message, nameof(Representative), rep.RepresentativeId, ct);
        }
    }

    /// <summary>Trigger 8 — approximated (see NotificationOptions.SupplierPaymentDueDays): neither
    /// PurchaseOrder nor SupplierPayment has an actual due-date field today, so this treats a goods
    /// receipt whose cost is still unpaid after N days as "payment due," the same net-terms convention
    /// used nowhere else in the app but the closest honest reading of the existing schema.</summary>
    private static async Task ScanSupplierPaymentsDueAsync(IAppDbContext db, IUserDirectoryService userDirectory,
        INotificationService notificationService, NotificationOptions settings, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-settings.SupplierPaymentDueDays);

        var suppliers = await db.Suppliers.AsNoTracking().Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.Name }).ToListAsync(ct);

        var recipientUserIds = (await userDirectory.GetUserIdsInRoleAsync(Roles.Procurement, ct))
            .Concat(await userDirectory.GetUserIdsInRoleAsync(Roles.Finance, ct))
            .Distinct().ToList();

        foreach (var supplier in suppliers)
        {
            var totalReceived = await db.PurchaseReceiptLines.AsNoTracking()
                .Where(l => !l.IsDeleted && l.PurchaseReceipt.PurchaseOrder.SupplierId == supplier.Id)
                .SumAsync(l => (decimal?)(l.QuantityReceived * l.PurchaseOrderLine.UnitCost), ct) ?? 0m;
            var totalPaid = await db.SupplierPayments.AsNoTracking()
                .Where(p => !p.IsDeleted && p.SupplierId == supplier.Id)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
            var outstanding = totalReceived - totalPaid;
            if (outstanding <= 0) continue;

            var oldestUnpaidReceiptDate = await db.PurchaseReceipts.AsNoTracking()
                .Where(r => !r.IsDeleted && r.PurchaseOrder.SupplierId == supplier.Id)
                .OrderBy(r => r.ReceiptDateUtc)
                .Select(r => (DateTime?)r.ReceiptDateUtc)
                .FirstOrDefaultAsync(ct);
            if (oldestUnpaidReceiptDate is not { } receiptDate || receiptDate > cutoff) continue;

            var message = $"{supplier.Name} has an outstanding payable of {outstanding:C} from a receipt on {receiptDate:yyyy-MM-dd}.";
            foreach (var userId in recipientUserIds)
                await notificationService.CreateIfNotAlreadyNotifiedTodayAsync(userId, NotificationTypes.SupplierPaymentDue,
                    message, nameof(Supplier), supplier.Id, ct);
        }
    }

    private static async Task<string?> GetDistrictManagerUserIdForRepresentativeAsync(IAppDbContext db, int representativeId, CancellationToken ct)
    {
        var territoryId = await db.Representatives.AsNoTracking()
            .Where(r => r.Id == representativeId).Select(r => r.TerritoryId).FirstOrDefaultAsync(ct);
        if (territoryId is not { } id) return null;

        return await db.Territories.AsNoTracking().Where(t => t.Id == id)
            .Select(t => t.DistrictManager != null ? t.DistrictManager.ApplicationUserId : null)
            .FirstOrDefaultAsync(ct);
    }
}

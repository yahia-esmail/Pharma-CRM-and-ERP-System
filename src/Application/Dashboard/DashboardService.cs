using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Performance;
using PharmaERP.Application.Sales;
using PharmaERP.Application.Warehouses;

namespace PharmaERP.Application.Dashboard;

public class DashboardService(
    IAppDbContext db,
    ISalesService salesService,
    ICollectionService collectionService,
    IPerformanceService performanceService,
    IWarehouseService warehouseService) : IDashboardService
{
    /// <summary>A representative's oldest unremitted collection older than this is flagged (spec 4.6 — "configurable threshold").</summary>
    private const int OverdueRemittanceDays = 7;

    public async Task<DashboardSummaryDto> GetSummaryAsync(int? territoryId, int year, int month, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var lastMonthStart = monthStart.AddMonths(-1);
        var lastMonthEnd = monthStart.AddDays(-1);

        var totalSalesThisMonth = await salesService.GetTotalSalesAsync(territoryId, monthStart, monthEnd, ct);
        var totalSalesLastMonth = await salesService.GetTotalSalesAsync(territoryId, lastMonthStart, lastMonthEnd, ct);
        var salesTrend = await GetSalesTrendAsync(territoryId, monthStart, 6, ct);

        var custodySummary = await collectionService.GetFinancialCustodySummaryAsync(territoryId, ct);
        var totalCollectedThisMonth = await db.Collections.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CollectionDateUtc >= monthStart.ToDateTime(TimeOnly.MinValue)
                        && c.CollectionDateUtc <= monthEnd.ToDateTime(TimeOnly.MaxValue)
                        && (territoryId == null || c.Representative.TerritoryId == territoryId))
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        var cutoff = DateTime.UtcNow.AddDays(-OverdueRemittanceDays);
        var overdueRemittanceCount = custodySummary.Count(c =>
            c.OutstandingBalance > 0 && c.OldestUnremittedCollectionDateUtc is { } d && d <= cutoff);

        var scorecards = await performanceService.GetScorecardsAsync(territoryId, year, month, ct);
        var avgCoverage = scorecards.Count > 0 ? Math.Round(scorecards.Average(s => s.VisitCoveragePercent), 1) : 0;

        var nearExpiry = await warehouseService.GetNearExpiryAsync(90, ct);
        var lowStock = await warehouseService.GetLowStockAsync(ct);

        var repsQuery = db.Representatives.AsNoTracking().Where(r => !r.IsDeleted);
        var doctorsQuery = db.Doctors.AsNoTracking().Where(d => !d.IsDeleted);
        var pharmaciesQuery = db.Pharmacies.AsNoTracking().Where(p => !p.IsDeleted);
        if (territoryId.HasValue)
        {
            repsQuery = repsQuery.Where(r => r.TerritoryId == territoryId);
            doctorsQuery = doctorsQuery.Where(d => d.TerritoryId == territoryId);
            pharmaciesQuery = pharmaciesQuery.Where(p => p.TerritoryId == territoryId);
        }

        var activeReps = await repsQuery.CountAsync(ct);
        var activeDoctors = await doctorsQuery.CountAsync(ct);
        var activePharmacies = await pharmaciesQuery.CountAsync(ct);

        var purchaseSpendThisMonth = await db.PurchaseReceiptLines.AsNoTracking()
            .Where(l => !l.IsDeleted && l.PurchaseReceipt.ReceiptDateUtc >= monthStart.ToDateTime(TimeOnly.MinValue)
                        && l.PurchaseReceipt.ReceiptDateUtc <= monthEnd.ToDateTime(TimeOnly.MaxValue))
            .SumAsync(l => (decimal?)(l.QuantityReceived * l.PurchaseOrderLine.UnitCost), ct) ?? 0m;

        var totalPurchased = await db.PurchaseReceiptLines.AsNoTracking().Where(l => !l.IsDeleted)
            .SumAsync(l => (decimal?)(l.QuantityReceived * l.PurchaseOrderLine.UnitCost), ct) ?? 0m;
        var totalPaid = await db.SupplierPayments.AsNoTracking().Where(p => !p.IsDeleted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new DashboardSummaryDto(
            year, month,
            totalSalesThisMonth, totalSalesLastMonth, salesTrend,
            totalCollectedThisMonth, custodySummary.Sum(c => c.OutstandingBalance), overdueRemittanceCount,
            avgCoverage, scorecards.Sum(s => s.PlannedVisits), scorecards.Sum(s => s.ActualVisits),
            activeReps, activeDoctors, activePharmacies,
            nearExpiry.Count, lowStock.Count,
            purchaseSpendThisMonth, totalPurchased - totalPaid,
            scorecards.OrderByDescending(s => s.VisitCoveragePercent).Take(5).ToList(),
            nearExpiry.OrderBy(e => e.DaysUntilExpiry).Take(5).ToList());
    }

    private async Task<IReadOnlyList<MonthlyTrendPointDto>> GetSalesTrendAsync(int? territoryId, DateOnly currentMonthStart,
        int monthsBack, CancellationToken ct)
    {
        var points = new List<MonthlyTrendPointDto>();
        for (var i = monthsBack - 1; i >= 0; i--)
        {
            var start = currentMonthStart.AddMonths(-i);
            var end = start.AddMonths(1).AddDays(-1);
            var total = await salesService.GetTotalSalesAsync(territoryId, start, end, ct);
            points.Add(new MonthlyTrendPointDto(start.Year, start.Month, total));
        }
        return points;
    }
}

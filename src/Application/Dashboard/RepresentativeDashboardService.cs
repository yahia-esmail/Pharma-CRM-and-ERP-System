using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Performance;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Dashboard;

public class RepresentativeDashboardService(IAppDbContext db, ICustodyService custodyService,
    ICollectionService collectionService) : IRepresentativeDashboardService
{
    public async Task<RepresentativeDashboardDto> GetMineAsync(int representativeId, CancellationToken ct = default)
    {
        var repExists = await db.Representatives.AnyAsync(r => r.Id == representativeId && !r.IsDeleted, ct);
        if (!repExists) throw new NotFoundException(nameof(Representative), representativeId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStartUtc = today.ToDateTime(TimeOnly.MinValue);
        var todayEndUtc = today.ToDateTime(TimeOnly.MaxValue);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue);

        var plannedVisitsToday = await db.VisitPlanItems.AsNoTracking()
            .Where(i => !i.IsDeleted && i.PlannedDate == today && i.VisitPlan.RepresentativeId == representativeId
                        && (i.VisitPlan.Status == VisitPlanStatus.Approved || i.VisitPlan.Status == VisitPlanStatus.Submitted))
            .CountAsync(ct);

        var completedDoctorVisitsToday = await db.DoctorVisits.AsNoTracking()
            .CountAsync(v => v.RepresentativeId == representativeId && v.VisitDateUtc >= todayStartUtc && v.VisitDateUtc <= todayEndUtc, ct);
        var completedPharmacyVisitsToday = await db.PharmacyVisits.AsNoTracking()
            .CountAsync(v => v.RepresentativeId == representativeId && v.VisitDateUtc >= todayStartUtc && v.VisitDateUtc <= todayEndUtc, ct);

        var ordersToday = await db.Orders.AsNoTracking()
            .CountAsync(o => !o.IsDeleted && o.RepresentativeId == representativeId
                              && o.OrderDateUtc >= todayStartUtc && o.OrderDateUtc <= todayEndUtc, ct);

        var salesToday = await db.Sales.AsNoTracking()
            .Where(s => !s.IsDeleted && s.RepresentativeId == representativeId
                        && s.SaleDateUtc >= todayStartUtc && s.SaleDateUtc <= todayEndUtc)
            .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;

        var salesMonthToDate = await db.Sales.AsNoTracking()
            .Where(s => !s.IsDeleted && s.RepresentativeId == representativeId
                        && s.SaleDateUtc >= monthStartUtc && s.SaleDateUtc <= todayEndUtc)
            .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;

        var collectionsToday = await db.Collections.AsNoTracking()
            .Where(c => !c.IsDeleted && c.RepresentativeId == representativeId
                        && c.CollectionDateUtc >= todayStartUtc && c.CollectionDateUtc <= todayEndUtc)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        var salesTarget = await db.PerformanceTargets.AsNoTracking()
            .Where(t => !t.IsDeleted && t.RepresentativeId == representativeId && t.Year == today.Year
                        && t.Month == today.Month && t.MetricKey == PerformanceMetricKeys.Sales)
            .Select(t => (decimal?)t.TargetValue)
            .FirstOrDefaultAsync(ct);

        var achievementPercent = salesTarget is { } target && target > 0
            ? Math.Round((double)(salesMonthToDate / target * 100), 1)
            : (double?)null;

        var stockCustodySnapshot = await custodyService.GetBalancesAsync(representativeId, ct);
        var financialCustodySnapshot = await collectionService.GetFinancialCustodyAsync(representativeId, ct);

        return new RepresentativeDashboardDto(
            plannedVisitsToday, completedDoctorVisitsToday + completedPharmacyVisitsToday, ordersToday,
            salesToday, collectionsToday, salesMonthToDate, salesTarget, achievementPercent,
            stockCustodySnapshot, financialCustodySnapshot);
    }
}

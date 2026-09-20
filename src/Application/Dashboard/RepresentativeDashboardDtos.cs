using PharmaERP.Application.Collections;
using PharmaERP.Application.Custody;

namespace PharmaERP.Application.Dashboard;

/// <summary>Home-screen summary for the mobile app (addendum 3.11) — a representative's own daily/monthly
/// figures, distinct from IDashboardService's territory-scoped executive summary.</summary>
public record RepresentativeDashboardDto(
    int PlannedVisitsToday,
    int CompletedVisitsToday,
    int OrdersToday,
    decimal SalesToday,
    decimal CollectionsToday,
    decimal SalesMonthToDate,
    decimal? SalesTarget,
    double? SalesTargetAchievementPercent,
    IReadOnlyList<RepStockCustodyBalanceDto> StockCustodySnapshot,
    RepFinancialCustodyDto FinancialCustodySnapshot);

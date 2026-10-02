using PharmaERP.Application.Performance;
using PharmaERP.Application.Warehouses;

namespace PharmaERP.Application.Dashboard;

public record MonthlyTrendPointDto(int Year, int Month, decimal TotalSales);

/// <summary>
/// Single-page rollup for the Executive Summary (spec 4.8) — every figure is composed from the
/// existing per-module services (Sales, Collections, Performance, Warehouses, Suppliers), never
/// re-derived here, so the dashboard can never drift from what each module's own screens show.
/// </summary>
public record DashboardSummaryDto(
    int Year,
    int Month,

    decimal TotalSalesThisMonth,
    decimal TotalSalesLastMonth,
    IReadOnlyList<MonthlyTrendPointDto> SalesTrend,

    decimal TotalCollectedThisMonth,
    decimal TotalOutstandingRepCustody,
    int RepsWithOverdueRemittance,

    double AverageVisitCoveragePercent,
    int TotalPlannedVisitsThisMonth,
    int TotalActualVisitsThisMonth,

    int ActiveRepresentativeCount,
    int ActiveDoctorCount,
    int ActivePharmacyCount,

    int NearExpiryBatchCount,
    int LowStockProductCount,

    decimal TotalPurchaseSpendThisMonth,
    decimal TotalOutstandingSupplierPayables,

    IReadOnlyList<PerformanceScorecardDto> TopPerformers,
    IReadOnlyList<NearExpiryBatchDto> UrgentExpiries);

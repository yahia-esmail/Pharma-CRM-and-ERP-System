namespace PharmaERP.Application.DistrictDashboard;

/// <summary>District Manager Dashboard (gap-analysis addendum 3.11) — distinct from the consolidated
/// Management Dashboard: strictly scoped to one territory's subtree, with a District -> Representative ->
/// Customer -> Transaction drill-down instead of a single flat summary.</summary>
public interface IDistrictDashboardService
{
    Task<DistrictDashboardDto> GetSummaryAsync(int territoryId, int year, int month, CancellationToken ct = default);

    Task<RepresentativeDrilldownDto> GetRepresentativeDrilldownAsync(int representativeId, int year, int month,
        CancellationToken ct = default);
}

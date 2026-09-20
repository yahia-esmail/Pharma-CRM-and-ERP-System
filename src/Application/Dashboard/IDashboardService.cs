namespace PharmaERP.Application.Dashboard;

public interface IDashboardService
{
    /// <summary>Role-scoped executive summary (spec 4.8) — pass a territoryId to scope it to one district/team.</summary>
    Task<DashboardSummaryDto> GetSummaryAsync(int? territoryId, int year, int month, CancellationToken ct = default);
}

namespace PharmaERP.Application.Dashboard;

public interface IRepresentativeDashboardService
{
    Task<RepresentativeDashboardDto> GetMineAsync(int representativeId, CancellationToken ct = default);
}

namespace PharmaERP.Application.Performance;

public record PerformanceScorecardDto(
    int RepresentativeId,
    string RepresentativeName,
    string? TerritoryName,
    int Year,
    int Month,
    int PlannedVisits,
    int ActualVisits,
    double VisitCoveragePercent,
    decimal? VisitCoverageTarget,
    int AssignedDoctorCount,
    int DoctorsVisitedCount,
    double DoctorCoveragePercent,
    double VisitQualityPercent);

public class PerformanceTargetSaveRequest
{
    public int RepresentativeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MetricKey { get; set; } = null!;
    public decimal TargetValue { get; set; }
}

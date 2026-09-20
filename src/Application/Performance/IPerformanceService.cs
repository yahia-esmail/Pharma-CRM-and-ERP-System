namespace PharmaERP.Application.Performance;

public static class PerformanceMetricKeys
{
    /// <summary>Metric key used for the visit-coverage target (spec 4.2 KPI: "visit coverage %").</summary>
    public const string VisitCoverage = "VisitCoverage";

    /// <summary>Metric key used for a representative's monthly sales target (mobile dashboard's "Target
    /// Achievement %", addendum 3.11) — a plain currency amount, not a percentage.</summary>
    public const string Sales = "Sales";
}

public interface IPerformanceService
{
    Task<PerformanceScorecardDto> GetScorecardAsync(int representativeId, int year, int month, CancellationToken ct = default);

    /// <summary>Scorecards for every representative in scope (all, or one territory) — feeds the leaderboard/dashboard.</summary>
    Task<IReadOnlyList<PerformanceScorecardDto>> GetScorecardsAsync(int? territoryId, int year, int month, CancellationToken ct = default);

    Task SetTargetAsync(PerformanceTargetSaveRequest request, CancellationToken ct = default);
}

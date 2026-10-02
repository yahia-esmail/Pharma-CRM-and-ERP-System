using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Pilot;

/// <summary>One representative's field-app figures over the pilot period (plan phase 12). Percentages are 0–100;
/// null when there's nothing to measure (no visits, no fixes reported).</summary>
public record PilotRepRow(
    int RepresentativeId,
    string Name,
    string? Territory,
    int Visits,
    int ActiveDays,
    double? WithLocationPct,
    double? MedianAccuracyMeters,
    double? P90AccuracyMeters,
    double? Within30mPct,
    double? MedianFixSeconds,
    double? Within10sPct,
    double? MismatchPct,
    double? OutsideTerritoryPct,
    double? TooShortPct,
    double? ClockSuspectPct,
    int OutsideGeofenceReasons,
    int LeftOpen,
    double? AverageDurationMinutes,
    int TrackPoints,
    double? AnomalyPct,
    int LocationProposals);

public record PilotReasonCount(string Reason, int Count);

public record PilotReport(DateOnly From, DateOnly To, IReadOnlyList<PilotRepRow> Rows, PilotRepRow Total,
    IReadOnlyList<PilotReasonCount> TopOutsideReasons)
{
    /// <summary>Plan 7.1: ≥ 90 % of check-ins within 30 m, and ≤ 10 s to a fix in 90 % of cases.</summary>
    public const double AccuracyTargetPct = 90, FixTimeTargetPct = 90;
}

public interface IPilotReportService
{
    Task<PilotReport> GetAsync(DateOnly from, DateOnly to, int? territoryId, CancellationToken ct = default);
}

/// <summary>What the pilot week is judged on (plan 7.1 / phase 12): check-in accuracy and time to a fix, how often
/// visits get flagged (location mismatch, outside territory, too short, device clock), visits left open, and the
/// foreground tracking volume. Read-only over the visits, location pings and location proposals.</summary>
public class PilotReportService(IAppDbContext db, IBusinessCalendar calendar, TimeProvider time) : IPilotReportService
{
    private sealed record VisitRow(int Rep, DateTime At, VisitSessionStatus Status, double? Lat, double? Accuracy, int? FixMs,
        bool Mismatch, bool Outside, bool TooShort, bool Clock, string? Reason, int? Duration);

    public async Task<PilotReport> GetAsync(DateOnly from, DateOnly to, int? territoryId, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);
        var fromUtc = calendar.StartOfDayUtc(from);
        var toUtc = calendar.StartOfDayUtc(to.AddDays(1));

        var reps = await db.Representatives.AsNoTracking()
            .Where(r => !r.IsDeleted && (territoryId == null || r.TerritoryId == territoryId))
            .Select(r => new { r.Id, r.FullName, Territory = r.Territory != null ? r.Territory.Name : null })
            .ToListAsync(ct);
        var repIds = reps.Select(r => r.Id).ToList();

        var visits = (await db.DoctorVisits.AsNoTracking()
                .Where(v => repIds.Contains(v.RepresentativeId) && v.VisitDateUtc >= fromUtc && v.VisitDateUtc < toUtc && v.SessionStatus != VisitSessionStatus.Cancelled)
                .Select(v => new VisitRow(v.RepresentativeId, v.VisitDateUtc, v.SessionStatus, v.CheckInLatitude, v.CheckInAccuracyMeters, v.CheckInFixElapsedMs,
                    v.LocationMismatch, v.OutsideTerritory, v.DurationTooShort, v.DeviceClockSuspect, v.OutsideGeofenceReason, v.DurationMinutes))
                .ToListAsync(ct))
            .Concat(await db.PharmacyVisits.AsNoTracking()
                .Where(v => repIds.Contains(v.RepresentativeId) && v.VisitDateUtc >= fromUtc && v.VisitDateUtc < toUtc && v.SessionStatus != VisitSessionStatus.Cancelled)
                .Select(v => new VisitRow(v.RepresentativeId, v.VisitDateUtc, v.SessionStatus, v.CheckInLatitude, v.CheckInAccuracyMeters, v.CheckInFixElapsedMs,
                    v.LocationMismatch, v.OutsideTerritory, v.DurationTooShort, v.DeviceClockSuspect, v.OutsideGeofenceReason, v.DurationMinutes))
                .ToListAsync(ct))
            .ToList();

        var pings = await db.LocationPings.AsNoTracking()
            .Where(p => repIds.Contains(p.RepresentativeId) && p.TimestampUtc >= fromUtc && p.TimestampUtc < toUtc)
            .GroupBy(p => p.RepresentativeId)
            .Select(g => new { Rep = g.Key, Count = g.Count(), Anomalies = g.Count(p => p.IsAnomaly) })
            .ToListAsync(ct);
        var proposals = await db.CustomerLocationProposals.AsNoTracking()
            .Where(p => repIds.Contains(p.RepresentativeId) && p.CapturedAtUtc >= fromUtc && p.CapturedAtUtc < toUtc)
            .GroupBy(p => p.RepresentativeId)
            .Select(g => new { Rep = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // A visit still open 12 h after check-in was forgotten (the rep never checked out).
        var staleBefore = time.GetUtcNow().UtcDateTime.AddHours(-12);

        PilotRepRow Build(int id, string name, string? territory, IReadOnlyList<VisitRow> v, int trackPoints, int anomalies, int proposalCount)
        {
            var accuracies = v.Where(x => x.Lat is not null && x.Accuracy is not null).Select(x => x.Accuracy!.Value).Order().ToList();
            var fixSeconds = v.Where(x => x.FixMs is not null).Select(x => x.FixMs!.Value / 1000.0).Order().ToList();
            var completed = v.Where(x => x.Status == VisitSessionStatus.Completed).ToList();
            return new PilotRepRow(id, name, territory,
                v.Count,
                v.Select(x => calendar.DateOf(x.At)).Distinct().Count(),
                Pct(v.Count(x => x.Lat is not null), v.Count),
                Percentile(accuracies, 0.5), Percentile(accuracies, 0.9),
                Pct(accuracies.Count(a => a <= 30), accuracies.Count),
                Percentile(fixSeconds, 0.5),
                Pct(fixSeconds.Count(s => s <= 10), fixSeconds.Count),
                Pct(v.Count(x => x.Mismatch), v.Count),
                Pct(v.Count(x => x.Outside), v.Count),
                Pct(completed.Count(x => x.TooShort), completed.Count),
                Pct(v.Count(x => x.Clock), v.Count),
                v.Count(x => !string.IsNullOrWhiteSpace(x.Reason)),
                v.Count(x => x.Status == VisitSessionStatus.Open && x.At < staleBefore),
                completed.Count(x => x.Duration is not null) is > 0 and var n ? Math.Round(completed.Where(x => x.Duration is not null).Average(x => x.Duration!.Value), 1) : null,
                trackPoints,
                Pct(anomalies, trackPoints),
                proposalCount);
        }

        var rows = reps
            .Select(r =>
            {
                var p = pings.FirstOrDefault(x => x.Rep == r.Id);
                return Build(r.Id, r.FullName, r.Territory, visits.Where(v => v.Rep == r.Id).ToList(),
                    p?.Count ?? 0, p?.Anomalies ?? 0, proposals.FirstOrDefault(x => x.Rep == r.Id)?.Count ?? 0);
            })
            .Where(r => r.Visits > 0 || r.TrackPoints > 0 || r.LocationProposals > 0)
            .OrderByDescending(r => r.Visits)
            .ToList();

        var total = Build(0, "All representatives", null, visits, pings.Sum(p => p.Count), pings.Sum(p => p.Anomalies), proposals.Sum(p => p.Count));
        var reasons = visits.Where(v => !string.IsNullOrWhiteSpace(v.Reason))
            .GroupBy(v => v.Reason!.StartsWith("Other:", StringComparison.Ordinal) ? "Other" : v.Reason!)
            .Select(g => new PilotReasonCount(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToList();
        return new PilotReport(from, to, rows, total, reasons);
    }

    private static double? Pct(int part, int whole) => whole == 0 ? null : Math.Round(part * 100.0 / whole, 1);

    /// <summary>Nearest-rank percentile of an ascending list.</summary>
    private static double? Percentile(IReadOnlyList<double> sorted, double p) =>
        sorted.Count == 0 ? null : Math.Round(sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)], 1);
}

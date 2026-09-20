using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Performance;

public class PerformanceService(IAppDbContext db) : IPerformanceService
{
    public async Task<PerformanceScorecardDto> GetScorecardAsync(int representativeId, int year, int month,
        CancellationToken ct = default)
    {
        var rep = await db.Representatives.AsNoTracking()
            .Include(r => r.Territory)
            .FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct);

        return await BuildScorecardAsync(rep, year, month, ct)
               ?? throw new Common.NotFoundException(nameof(Representative), representativeId);
    }

    public async Task<IReadOnlyList<PerformanceScorecardDto>> GetScorecardsAsync(int? territoryId, int year,
        int month, CancellationToken ct = default)
    {
        var repsQuery = db.Representatives.AsNoTracking().Include(r => r.Territory).Where(r => !r.IsDeleted);
        if (territoryId.HasValue)
            repsQuery = repsQuery.Where(r => r.TerritoryId == territoryId);

        var reps = await repsQuery.OrderBy(r => r.FullName).ToListAsync(ct);

        var scorecards = new List<PerformanceScorecardDto>(reps.Count);
        foreach (var rep in reps)
        {
            var card = await BuildScorecardAsync(rep, year, month, ct);
            if (card is not null) scorecards.Add(card);
        }

        return scorecards.OrderByDescending(s => s.VisitCoveragePercent).ToList();
    }

    public async Task SetTargetAsync(PerformanceTargetSaveRequest request, CancellationToken ct = default)
    {
        var target = await db.PerformanceTargets.FirstOrDefaultAsync(t =>
            t.RepresentativeId == request.RepresentativeId && t.Year == request.Year && t.Month == request.Month
            && t.MetricKey == request.MetricKey && !t.IsDeleted, ct);

        if (target is null)
        {
            db.PerformanceTargets.Add(new PerformanceTarget
            {
                RepresentativeId = request.RepresentativeId,
                Year = request.Year,
                Month = request.Month,
                MetricKey = request.MetricKey,
                TargetValue = request.TargetValue
            });
        }
        else
        {
            // Targets are versioned per period (spec 4.2) — a period already has a row, so update it
            // in place rather than creating a duplicate for the same (rep, period, metric) triple.
            target.TargetValue = request.TargetValue;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<PerformanceScorecardDto?> BuildScorecardAsync(Representative? rep, int year, int month,
        CancellationToken ct)
    {
        if (rep is null) return null;

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue);
        var monthEndUtc = monthEnd.ToDateTime(TimeOnly.MaxValue);

        var plannedCount = await db.VisitPlanItems.AsNoTracking()
            .Where(i => !i.IsDeleted && i.VisitPlan.RepresentativeId == rep.Id
                        && i.PlannedDate >= monthStart && i.PlannedDate <= monthEnd
                        && (i.VisitPlan.Status == VisitPlanStatus.Approved || i.VisitPlan.Status == VisitPlanStatus.Submitted))
            .CountAsync(ct);

        var actualVisits = await db.DoctorVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == rep.Id && v.VisitDateUtc >= monthStartUtc && v.VisitDateUtc <= monthEndUtc)
            .Select(v => v.DoctorId)
            .ToListAsync(ct);

        var assignedDoctorCount = await db.Doctors.AsNoTracking()
            .CountAsync(d => !d.IsDeleted && d.PrimaryRepresentativeId == rep.Id, ct);

        var target = await db.PerformanceTargets.AsNoTracking()
            .Where(t => !t.IsDeleted && t.RepresentativeId == rep.Id && t.Year == year && t.Month == month
                        && t.MetricKey == PerformanceMetricKeys.VisitCoverage)
            .Select(t => (decimal?)t.TargetValue)
            .FirstOrDefaultAsync(ct);

        var doctorsVisitedCount = actualVisits.Distinct().Count();
        var visitCoverage = plannedCount == 0 ? 0 : Math.Round(actualVisits.Count * 100.0 / plannedCount, 1);
        var doctorCoverage = assignedDoctorCount == 0 ? 0 : Math.Round(doctorsVisitedCount * 100.0 / assignedDoctorCount, 1);

        // Visit Quality indicator (addendum 3.3) — the share of this month's visits (doctor + pharmacy)
        // that passed every Visit Validation rule cleanly, i.e. raised none of the LocationMismatch /
        // OutsideTerritory / DurationTooShort flags.
        var doctorVisitFlags = await db.DoctorVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == rep.Id && v.VisitDateUtc >= monthStartUtc && v.VisitDateUtc <= monthEndUtc)
            .Select(v => v.LocationMismatch || v.OutsideTerritory || v.DurationTooShort)
            .ToListAsync(ct);
        var pharmacyVisitFlags = await db.PharmacyVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == rep.Id && v.VisitDateUtc >= monthStartUtc && v.VisitDateUtc <= monthEndUtc)
            .Select(v => v.LocationMismatch || v.OutsideTerritory || v.DurationTooShort)
            .ToListAsync(ct);
        var allVisitFlags = doctorVisitFlags.Concat(pharmacyVisitFlags).ToList();
        var visitQuality = allVisitFlags.Count == 0 ? 100.0 : Math.Round(allVisitFlags.Count(flagged => !flagged) * 100.0 / allVisitFlags.Count, 1);

        return new PerformanceScorecardDto(rep.Id, rep.FullName, rep.Territory?.Name, year, month, plannedCount,
            actualVisits.Count, visitCoverage, target, assignedDoctorCount, doctorsVisitedCount, doctorCoverage,
            visitQuality);
    }
}

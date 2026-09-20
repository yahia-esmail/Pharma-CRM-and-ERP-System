using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Performance;
using PharmaERP.Application.Territories;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.DistrictDashboard;

public class DistrictDashboardService(
    IAppDbContext db,
    ITerritoryService territoryService,
    IPerformanceService performanceService,
    ICollectionService collectionService) : IDistrictDashboardService
{
    public async Task<DistrictDashboardDto> GetSummaryAsync(int territoryId, int year, int month, CancellationToken ct = default)
    {
        var territory = await db.Territories.AsNoTracking().FirstOrDefaultAsync(t => t.Id == territoryId && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Territory), territoryId);

        // Unlike the Management Dashboard's flat TerritoryId equality, a district manager's own dashboard
        // must include every representative in the territory's full subtree (addendum 3.11's "District"
        // level covers everything beneath it, not just reps assigned to this exact node).
        var subtreeIds = await territoryService.GetDescendantTerritoryIdsAsync(territoryId, ct);

        var reps = await db.Representatives.AsNoTracking()
            .Where(r => !r.IsDeleted && r.TerritoryId != null && subtreeIds.Contains(r.TerritoryId.Value))
            .OrderBy(r => r.FullName)
            .ToListAsync(ct);

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue);
        var monthEndUtc = monthEnd.ToDateTime(TimeOnly.MaxValue);

        var summaries = new List<DistrictRepresentativeSummaryDto>(reps.Count);
        foreach (var rep in reps)
        {
            var scorecard = await performanceService.GetScorecardAsync(rep.Id, year, month, ct);
            var salesThisMonth = await db.Sales.AsNoTracking()
                .Where(s => !s.IsDeleted && s.RepresentativeId == rep.Id
                    && s.SaleDateUtc >= monthStartUtc && s.SaleDateUtc <= monthEndUtc)
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
            var custody = await collectionService.GetFinancialCustodyAsync(rep.Id, ct);

            summaries.Add(new DistrictRepresentativeSummaryDto(rep.Id, rep.FullName, scorecard.PlannedVisits,
                scorecard.ActualVisits, scorecard.VisitCoveragePercent, scorecard.VisitQualityPercent,
                salesThisMonth, custody.OutstandingBalance));
        }

        var totalCollected = await db.Collections.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Representative.TerritoryId != null && subtreeIds.Contains(c.Representative.TerritoryId.Value)
                && c.CollectionDateUtc >= monthStartUtc && c.CollectionDateUtc <= monthEndUtc)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return new DistrictDashboardDto(territory.Id, territory.Name, year, month,
            summaries.Sum(s => s.SalesThisMonth), totalCollected, summaries.Sum(s => s.OutstandingCustody),
            summaries.Count > 0 ? Math.Round(summaries.Average(s => s.VisitCoveragePercent), 1) : 0,
            summaries.Count > 0 ? Math.Round(summaries.Average(s => s.VisitQualityPercent), 1) : 100,
            summaries.OrderByDescending(s => s.VisitCoveragePercent).ToList());
    }

    public async Task<RepresentativeDrilldownDto> GetRepresentativeDrilldownAsync(int representativeId, int year, int month,
        CancellationToken ct = default)
    {
        var rep = await db.Representatives.AsNoTracking().Include(r => r.Territory)
            .FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), representativeId);

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue);
        var monthEndUtc = monthEnd.ToDateTime(TimeOnly.MaxValue);

        var doctors = await db.Doctors.AsNoTracking()
            .Where(d => !d.IsDeleted && d.PrimaryRepresentativeId == representativeId)
            .OrderBy(d => d.FullName)
            .Select(d => new { d.Id, d.FullName })
            .ToListAsync(ct);

        var doctorDrilldowns = new List<CustomerDrilldownDto>(doctors.Count);
        foreach (var d in doctors)
        {
            var visits = await db.DoctorVisits.AsNoTracking()
                .CountAsync(v => !v.IsDeleted && v.DoctorId == d.Id && v.RepresentativeId == representativeId
                    && v.VisitDateUtc >= monthStartUtc && v.VisitDateUtc <= monthEndUtc, ct);
            doctorDrilldowns.Add(new CustomerDrilldownDto("Doctor", d.Id, d.FullName, visits, null, null));
        }

        var pharmacies = await db.Pharmacies.AsNoTracking()
            .Where(p => !p.IsDeleted && p.PrimaryRepresentativeId == representativeId)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var pharmacyDrilldowns = new List<CustomerDrilldownDto>(pharmacies.Count);
        var pharmacySalesThisMonth = 0m;
        foreach (var p in pharmacies)
        {
            var visits = await db.PharmacyVisits.AsNoTracking()
                .CountAsync(v => !v.IsDeleted && v.PharmacyId == p.Id && v.RepresentativeId == representativeId
                    && v.VisitDateUtc >= monthStartUtc && v.VisitDateUtc <= monthEndUtc, ct);
            var sales = await db.Sales.AsNoTracking()
                .Where(s => !s.IsDeleted && s.PharmacyId == p.Id && s.RepresentativeId == representativeId
                    && s.SaleDateUtc >= monthStartUtc && s.SaleDateUtc <= monthEndUtc)
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
            var outstanding = await db.Sales.AsNoTracking().Where(s => !s.IsDeleted && s.PharmacyId == p.Id)
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
            var collected = await db.Collections.AsNoTracking().Where(c => !c.IsDeleted && c.PharmacyId == p.Id)
                .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

            pharmacySalesThisMonth += sales;
            pharmacyDrilldowns.Add(new CustomerDrilldownDto("Pharmacy", p.Id, p.Name, visits, sales, outstanding - collected));
        }

        var custody = await collectionService.GetFinancialCustodyAsync(representativeId, ct);

        return new RepresentativeDrilldownDto(rep.Id, rep.FullName, rep.Territory?.Name, year, month,
            pharmacySalesThisMonth, custody.OutstandingBalance, doctorDrilldowns, pharmacyDrilldowns);
    }
}

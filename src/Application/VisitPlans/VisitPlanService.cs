using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.VisitPlans;

public class VisitPlanService(IAppDbContext db, ICurrentUserService currentUser) : IVisitPlanService
{
    public async Task<PagedResult<VisitPlanListItemDto>> GetListAsync(PagedRequest request, int? representativeId,
        VisitPlanStatus? status, CancellationToken ct = default)
    {
        var query = db.VisitPlans.AsNoTracking().Where(p => !p.IsDeleted);

        if (!currentUser.HasUnrestrictedAccess)
        {
            query = currentUser.TerritoryId is { } scopedTerritoryId
                ? query.Where(p => p.Representative.TerritoryId == scopedTerritoryId)
                : query.Where(p => p.RepresentativeId == currentUser.RepresentativeId);
        }

        if (representativeId.HasValue)
            query = query.Where(p => p.RepresentativeId == representativeId);
        if (status.HasValue)
            query = query.Where(p => p.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new VisitPlanListItemDto(p.Id, p.RepresentativeId, p.Representative.FullName,
                p.PeriodType, p.StartDate, p.EndDate, p.Status, p.Items.Count(i => !i.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<VisitPlanListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<VisitPlanDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var plan = await db.VisitPlans.AsNoTracking()
            .Include(p => p.Representative)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .ThenInclude(i => i.Doctor)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .ThenInclude(i => i.Pharmacy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlan), id);

        return ToDetailDto(plan);
    }

    public async Task<int> CreateDraftAsync(VisitPlanCreateRequest request, CancellationToken ct = default)
    {
        if (request.EndDate < request.StartDate)
            throw new ValidationFailedException("End date cannot be before the start date.");

        var plan = new VisitPlan
        {
            RepresentativeId = request.RepresentativeId,
            PeriodType = request.PeriodType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = VisitPlanStatus.Draft
        };
        db.VisitPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan.Id;
    }

    public async Task<int> AddItemAsync(int visitPlanId, VisitPlanItemSaveRequest request, CancellationToken ct = default)
    {
        var plan = await LoadEditablePlanAsync(visitPlanId, ct);

        if (request.PlannedDate < plan.StartDate || request.PlannedDate > plan.EndDate)
            throw new ValidationFailedException("Planned date must fall within the plan's period.");

        if (request.DoctorId is null == request.PharmacyId is null)
            throw new ValidationFailedException("Specify either a doctor or a pharmacy for this stop, not both or neither.");

        if (request.DoctorId is { } doctorId && !await db.Doctors.AnyAsync(d => d.Id == doctorId && !d.IsDeleted, ct))
            throw new NotFoundException(nameof(Doctor), doctorId);
        if (request.PharmacyId is { } pharmacyId && !await db.Pharmacies.AnyAsync(p => p.Id == pharmacyId && !p.IsDeleted, ct))
            throw new NotFoundException(nameof(Pharmacy), pharmacyId);

        var item = new VisitPlanItem
        {
            VisitPlanId = visitPlanId,
            DoctorId = request.DoctorId,
            PharmacyId = request.PharmacyId,
            PlannedDate = request.PlannedDate,
            Sequence = request.Sequence,
            Notes = request.Notes
        };
        db.VisitPlanItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task RemoveItemAsync(int visitPlanId, int itemId, CancellationToken ct = default)
    {
        await LoadEditablePlanAsync(visitPlanId, ct);

        var item = await db.VisitPlanItems.FirstOrDefaultAsync(i => i.Id == itemId && i.VisitPlanId == visitPlanId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlanItem), itemId);

        item.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitAsync(int visitPlanId, CancellationToken ct = default)
    {
        var plan = await LoadEditablePlanAsync(visitPlanId, ct);

        var hasItems = await db.VisitPlanItems.AnyAsync(i => i.VisitPlanId == visitPlanId && !i.IsDeleted, ct);
        if (!hasItems)
            throw new ValidationFailedException("Add at least one planned visit before submitting.");

        plan.Status = VisitPlanStatus.Submitted;
        plan.SubmittedAtUtc = DateTime.UtcNow;
        plan.RejectionReason = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task ApproveAsync(int visitPlanId, string approverUserId, CancellationToken ct = default)
    {
        var plan = await db.VisitPlans.FirstOrDefaultAsync(p => p.Id == visitPlanId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlan), visitPlanId);

        if (plan.Status != VisitPlanStatus.Submitted)
            throw new ValidationFailedException("Only a submitted plan can be approved.");

        plan.Status = VisitPlanStatus.Approved;
        plan.ApprovedByUserId = approverUserId;
        plan.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int visitPlanId, string approverUserId, string reason, CancellationToken ct = default)
    {
        var plan = await db.VisitPlans.FirstOrDefaultAsync(p => p.Id == visitPlanId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlan), visitPlanId);

        if (plan.Status != VisitPlanStatus.Submitted)
            throw new ValidationFailedException("Only a submitted plan can be rejected.");

        plan.Status = VisitPlanStatus.Rejected;
        plan.ApprovedByUserId = approverUserId;
        plan.ApprovedAtUtc = DateTime.UtcNow;
        plan.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VisitPlanItemDto>> GetTodaysPlanAsync(int representativeId, DateOnly date,
        CancellationToken ct = default)
    {
        return await db.VisitPlanItems.AsNoTracking()
            .Where(i => !i.IsDeleted && i.PlannedDate == date
                        && i.VisitPlan.RepresentativeId == representativeId
                        && (i.VisitPlan.Status == VisitPlanStatus.Approved || i.VisitPlan.Status == VisitPlanStatus.Submitted))
            .Include(i => i.Doctor)
            .Include(i => i.Pharmacy)
            .OrderBy(i => i.Sequence)
            .Select(i => new VisitPlanItemDto(i.Id, i.DoctorId, i.Doctor != null ? i.Doctor.FullName : null,
                i.PharmacyId, i.Pharmacy != null ? i.Pharmacy.Name : null, i.PlannedDate, i.Sequence, i.Notes))
            .ToListAsync(ct);
    }

    public async Task<PlanVsActualDto> GetPlanVsActualAsync(int visitPlanId, CancellationToken ct = default)
    {
        var plan = await db.VisitPlans.AsNoTracking()
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .ThenInclude(i => i.Doctor)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .ThenInclude(i => i.Pharmacy)
            .FirstOrDefaultAsync(p => p.Id == visitPlanId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlan), visitPlanId);

        var actualDoctorVisits = await db.DoctorVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == plan.RepresentativeId
                        && v.VisitDateUtc.Date >= plan.StartDate.ToDateTime(TimeOnly.MinValue)
                        && v.VisitDateUtc.Date <= plan.EndDate.ToDateTime(TimeOnly.MinValue))
            .Select(v => new { v.DoctorId, v.VisitDateUtc })
            .ToListAsync(ct);

        var actualPharmacyVisits = await db.PharmacyVisits.AsNoTracking()
            .Where(v => v.RepresentativeId == plan.RepresentativeId
                        && v.VisitDateUtc.Date >= plan.StartDate.ToDateTime(TimeOnly.MinValue)
                        && v.VisitDateUtc.Date <= plan.EndDate.ToDateTime(TimeOnly.MinValue))
            .Select(v => new { v.PharmacyId, v.VisitDateUtc })
            .ToListAsync(ct);

        var items = plan.Items.OrderBy(i => i.PlannedDate).ThenBy(i => i.Sequence).Select(i =>
        {
            if (i.PharmacyId is { } pharmacyId)
            {
                var pharmacyMatch = actualPharmacyVisits.FirstOrDefault(v => v.PharmacyId == pharmacyId
                    && DateOnly.FromDateTime(v.VisitDateUtc) == i.PlannedDate);

                return new VisitPlanVarianceItemDto(null, null, pharmacyId, i.Pharmacy!.Name, i.PlannedDate,
                    pharmacyMatch is not null, pharmacyMatch?.VisitDateUtc);
            }

            var match = actualDoctorVisits.FirstOrDefault(v => v.DoctorId == i.DoctorId
                && DateOnly.FromDateTime(v.VisitDateUtc) == i.PlannedDate);

            return new VisitPlanVarianceItemDto(i.DoctorId, i.Doctor!.FullName, null, null, i.PlannedDate,
                match is not null, match?.VisitDateUtc);
        }).ToList();

        var visitedCount = items.Count(i => i.Visited);

        return new PlanVsActualDto(plan.Id, items.Count, visitedCount,
            items.Count == 0 ? 0 : Math.Round(visitedCount * 100.0 / items.Count, 1), items);
    }

    private async Task<VisitPlan> LoadEditablePlanAsync(int visitPlanId, CancellationToken ct)
    {
        var plan = await db.VisitPlans.FirstOrDefaultAsync(p => p.Id == visitPlanId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitPlan), visitPlanId);

        if (plan.Status is not (VisitPlanStatus.Draft or VisitPlanStatus.Rejected))
            throw new ValidationFailedException("Only a draft or rejected plan can be edited.");

        return plan;
    }

    private static VisitPlanDetailDto ToDetailDto(VisitPlan plan) => new(
        plan.Id, plan.RepresentativeId, plan.Representative.FullName, plan.PeriodType, plan.StartDate,
        plan.EndDate, plan.Status, plan.SubmittedAtUtc, plan.ApprovedByUserId, plan.ApprovedAtUtc,
        plan.RejectionReason,
        plan.Items.OrderBy(i => i.PlannedDate).ThenBy(i => i.Sequence)
            .Select(i => new VisitPlanItemDto(i.Id, i.DoctorId, i.Doctor?.FullName,
                i.PharmacyId, i.Pharmacy?.Name, i.PlannedDate, i.Sequence, i.Notes))
            .ToList());
}

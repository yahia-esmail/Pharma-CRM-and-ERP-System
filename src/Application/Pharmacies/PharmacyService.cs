using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Pharmacies;

public class PharmacyService(IAppDbContext db, ICurrentUserService currentUser, IVisitValidationService visitValidation,
    IBusinessCalendar calendar) : IPharmacyService
{
    public async Task<PagedResult<PharmacyListItemDto>> GetListAsync(PagedRequest request, int? territoryId,
        int? representativeId, CancellationToken ct = default)
    {
        var query = db.Pharmacies.AsNoTracking().Where(p => !p.IsDeleted);

        if (!currentUser.HasUnrestrictedAccess)
        {
            query = currentUser.TerritoryId is { } scopedTerritoryId
                ? query.Where(p => p.TerritoryId == scopedTerritoryId)
                : query.Where(p => p.PrimaryRepresentativeId == currentUser.RepresentativeId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(p => p.Name.Contains(request.Search));
        if (territoryId.HasValue) query = query.Where(p => p.TerritoryId == territoryId);
        if (representativeId.HasValue) query = query.Where(p => p.PrimaryRepresentativeId == representativeId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.City, p.Segment, p.Status, p.Latitude, p.Longitude,
                PrimaryRepresentativeName = p.PrimaryRepresentative != null ? p.PrimaryRepresentative.FullName : null,
                OutstandingBalance = db.Sales.Where(s => s.PharmacyId == p.Id).Sum(s => (decimal?)s.TotalAmount) ?? 0m
            })
            .ToListAsync(ct);

        return new PagedResult<PharmacyListItemDto>
        {
            Items = items.Select(p => new PharmacyListItemDto(p.Id, p.Name, p.City, p.Segment,
                p.PrimaryRepresentativeName, p.OutstandingBalance, p.Status, p.Latitude, p.Longitude)).ToList(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<PharmacyDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.AsNoTracking()
            .Include(p => p.PrimaryRepresentative)
            .Include(p => p.Territory)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), id);

        return ToDetailDto(pharmacy);
    }

    public async Task<int> CreateAsync(PharmacySaveRequest request, CancellationToken ct = default)
    {
        var pharmacy = new Pharmacy();
        Apply(pharmacy, request);
        db.Pharmacies.Add(pharmacy);
        await db.SaveChangesAsync(ct);
        return pharmacy.Id;
    }

    public async Task UpdateAsync(int id, PharmacySaveRequest request, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), id);

        Apply(pharmacy, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), id);

        pharmacy.Status = PharmacyStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PharmacyLedgerDto> GetLedgerAsync(int id, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), id);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.PharmacyId == id)
            .OrderByDescending(s => s.SaleDateUtc)
            .Select(s => new { s.Id, s.SaleDateUtc, s.TotalAmount })
            .ToListAsync(ct);

        var today = DateTime.UtcNow;
        var lines = sales.Select(s =>
        {
            var dueDate = s.SaleDateUtc.AddDays(pharmacy.PaymentTermDays);
            var daysOverdue = Math.Max(0, (today - dueDate).Days);
            return new PharmacyLedgerLineDto(s.Id, s.SaleDateUtc, dueDate, s.TotalAmount, daysOverdue);
        }).ToList();

        var totalSales = lines.Sum(l => l.Amount);
        // Collections module landed in Phase 5. Aging buckets below are still computed against gross
        // sales due dates (per-sale collection allocation would need an explicit settlement model);
        // the headline totals are accurate since they're the pharmacy's full collected-to-date figure.
        var totalCollected = await db.Collections.Where(c => c.PharmacyId == id).SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return new PharmacyLedgerDto(
            id, totalSales, totalCollected, totalSales - totalCollected,
            lines.Where(l => l.DaysOverdue is >= 0 and <= 30).Sum(l => l.Amount),
            lines.Where(l => l.DaysOverdue is > 30 and <= 60).Sum(l => l.Amount),
            lines.Where(l => l.DaysOverdue > 60).Sum(l => l.Amount),
            lines);
    }

    public async Task<IReadOnlyList<PharmacyVisitDto>> GetVisitsAsync(int pharmacyId, CancellationToken ct = default)
    {
        return await db.PharmacyVisits.AsNoTracking()
            .Where(v => v.PharmacyId == pharmacyId && !v.IsDeleted)
            .Include(v => v.Representative)
            .OrderByDescending(v => v.VisitDateUtc)
            .Select(v => new PharmacyVisitDto(v.Id, v.PharmacyId, v.RepresentativeId, v.Representative.FullName,
                v.VisitDateUtc, v.DurationMinutes, v.Purpose, v.Notes, v.CheckInLatitude, v.CheckInLongitude,
                v.VisitPlanItemId, v.IsPlanned, v.LocationMismatch, v.OutsideTerritory, v.DurationTooShort))
            .ToListAsync(ct);
    }

    public async Task<int> AddVisitAsync(int representativeId, PharmacyVisitSaveRequest request, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), request.PharmacyId);
        var representative = await db.Representatives.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), representativeId);

        var validation = await visitValidation.EvaluateAsync(representative.TerritoryId, pharmacy.TerritoryId,
            pharmacy.Latitude, pharmacy.Longitude, request.CheckInLatitude, request.CheckInLongitude,
            request.DurationMinutes, ct);

        var visitDate = calendar.DateOf(request.VisitDateUtc);
        int? visitPlanItemId;
        bool isPlanned;

        if (request.VisitPlanItemId is { } explicitItemId)
        {
            var item = await db.VisitPlanItems.AsNoTracking().Include(i => i.VisitPlan)
                .FirstOrDefaultAsync(i => i.Id == explicitItemId && !i.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(VisitPlanItem), explicitItemId);
            if (item.PharmacyId != request.PharmacyId || item.VisitPlan.RepresentativeId != representativeId)
                throw new ValidationFailedException("This planned visit does not belong to the specified pharmacy/representative.");

            visitPlanItemId = explicitItemId;
            isPlanned = item.VisitPlan.Status == VisitPlanStatus.Approved;
        }
        else
        {
            visitPlanItemId = await db.VisitPlanItems.AsNoTracking()
                .Where(i => !i.IsDeleted && i.PharmacyId == request.PharmacyId && i.PlannedDate == visitDate
                    && i.VisitPlan.RepresentativeId == representativeId && i.VisitPlan.Status == VisitPlanStatus.Approved)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync(ct);
            isPlanned = visitPlanItemId.HasValue;
        }

        var visit = new PharmacyVisit
        {
            PharmacyId = request.PharmacyId,
            RepresentativeId = representativeId,
            VisitDateUtc = request.VisitDateUtc,
            DurationMinutes = request.DurationMinutes,
            Purpose = request.Purpose,
            Notes = request.Notes,
            CheckInLatitude = request.CheckInLatitude,
            CheckInLongitude = request.CheckInLongitude,
            VisitPlanItemId = visitPlanItemId,
            IsPlanned = isPlanned,
            LocationMismatch = validation.LocationMismatch,
            OutsideTerritory = validation.OutsideTerritory,
            DurationTooShort = validation.DurationTooShort
        };
        db.PharmacyVisits.Add(visit);
        await db.SaveChangesAsync(ct);
        return visit.Id;
    }

    private static void Apply(Pharmacy pharmacy, PharmacySaveRequest request)
    {
        pharmacy.Name = request.Name;
        pharmacy.LicenseNumber = request.LicenseNumber;
        pharmacy.OwnerName = request.OwnerName;
        pharmacy.Address = request.Address;
        pharmacy.Governorate = request.Governorate;
        pharmacy.City = request.City;
        pharmacy.Phone = request.Phone;
        pharmacy.WhatsAppNumber = request.WhatsAppNumber;
        pharmacy.Email = request.Email;
        pharmacy.Segment = request.Segment;
        pharmacy.PaymentTermDays = request.PaymentTermDays;
        pharmacy.CreditLimit = request.CreditLimit;
        pharmacy.PrimaryRepresentativeId = request.PrimaryRepresentativeId;
        pharmacy.TerritoryId = request.TerritoryId;
        pharmacy.Status = request.Status;
        pharmacy.Latitude = request.Latitude;
        pharmacy.Longitude = request.Longitude;
    }

    private static PharmacyDetailDto ToDetailDto(Pharmacy p) => new(
        p.Id, p.Name, p.LicenseNumber, p.OwnerName, p.Address, p.Governorate, p.City, p.Phone, p.WhatsAppNumber, p.Email,
        p.Segment, p.PaymentTermDays, p.CreditLimit, p.PrimaryRepresentativeId, p.PrimaryRepresentative?.FullName,
        p.TerritoryId, p.Territory?.Name, p.Status, p.Latitude, p.Longitude);
}

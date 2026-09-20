using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Doctors;

public class DoctorService(IAppDbContext db, ICurrentUserService currentUser, IVisitValidationService visitValidation) : IDoctorService
{
    /// <summary>Visit records become read-only after this window (spec 4.1) — later edits must be logged as amendments.</summary>
    private static readonly TimeSpan VisitEditWindow = TimeSpan.FromHours(48);

    public async Task<PagedResult<DoctorListItemDto>> GetListAsync(PagedRequest request, string? specialty,
        string? city, int? classificationId, int? territoryId, int? representativeId, CancellationToken ct = default)
    {
        var query = db.Doctors.AsNoTracking().Where(d => !d.IsDeleted);

        if (!currentUser.HasUnrestrictedAccess)
        {
            query = currentUser.TerritoryId is { } scopedTerritoryId
                ? query.Where(d => d.TerritoryId == scopedTerritoryId)
                : query.Where(d => d.PrimaryRepresentativeId == currentUser.RepresentativeId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(d => d.FullName.Contains(request.Search) || d.ClinicOrHospital!.Contains(request.Search));
        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(d => d.Specialty == specialty);
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(d => d.City == city);
        if (classificationId.HasValue)
            query = query.Where(d => d.ClassificationId == classificationId);
        if (territoryId.HasValue)
            query = query.Where(d => d.TerritoryId == territoryId);
        if (representativeId.HasValue)
            query = query.Where(d => d.PrimaryRepresentativeId == representativeId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(d => d.FullName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DoctorListItemDto(
                d.Id, d.FullName, d.Specialty, d.City,
                d.Classification != null ? d.Classification.Name : null,
                d.PrimaryRepresentative != null ? d.PrimaryRepresentative.FullName : null,
                d.Status))
            .ToListAsync(ct);

        return new PagedResult<DoctorListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<DoctorDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking()
            .Include(d => d.Classification)
            .Include(d => d.PrimaryRepresentative)
            .Include(d => d.Territory)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), id);

        return ToDetailDto(doctor);
    }

    public async Task<IReadOnlyList<DuplicateDoctorMatch>> FindDuplicatesAsync(string fullName, string? phone,
        string? clinicOrHospital, CancellationToken ct = default)
    {
        var query = db.Doctors.AsNoTracking().Where(d => !d.IsDeleted && d.FullName == fullName);
        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(d => d.Phone == phone || d.ClinicOrHospital == clinicOrHospital);

        return await query
            .Select(d => new DuplicateDoctorMatch(d.Id, d.FullName, d.Phone, d.ClinicOrHospital))
            .Take(10)
            .ToListAsync(ct);
    }

    public async Task<int> CreateAsync(DoctorSaveRequest request, CancellationToken ct = default)
    {
        var doctor = new Doctor();
        Apply(doctor, request);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        return doctor.Id;
    }

    public async Task UpdateAsync(int id, DoctorSaveRequest request, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), id);

        Apply(doctor, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeClassificationAsync(int id, int? newClassificationId, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), id);

        // The generic audit interceptor (spec 4.11) captures old/new ClassificationId automatically on SaveChanges.
        doctor.ClassificationId = newClassificationId;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), id);

        doctor.Status = DoctorStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }

    public async Task MergeAsync(int survivingDoctorId, int duplicateDoctorId, CancellationToken ct = default)
    {
        if (survivingDoctorId == duplicateDoctorId)
            throw new ValidationFailedException("Cannot merge a doctor record with itself.");

        var duplicate = await db.Doctors.FirstOrDefaultAsync(d => d.Id == duplicateDoctorId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), duplicateDoctorId);
        _ = await db.Doctors.AnyAsync(d => d.Id == survivingDoctorId && !d.IsDeleted, ct)
            ? true
            : throw new NotFoundException(nameof(Doctor), survivingDoctorId);

        await db.DoctorVisits.Where(v => v.DoctorId == duplicateDoctorId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.DoctorId, survivingDoctorId), ct);
        await db.DoctorFollowUps.Where(f => f.DoctorId == duplicateDoctorId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.DoctorId, survivingDoctorId), ct);

        duplicate.Status = DoctorStatus.Inactive;
        duplicate.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorVisitDto>> GetVisitsAsync(int doctorId, CancellationToken ct = default)
    {
        return await db.DoctorVisits.AsNoTracking()
            .Where(v => v.DoctorId == doctorId && !v.IsDeleted)
            .Include(v => v.Representative)
            .OrderByDescending(v => v.VisitDateUtc)
            .Select(v => new DoctorVisitDto(v.Id, v.DoctorId, v.RepresentativeId, v.Representative.FullName,
                v.VisitDateUtc, v.DurationMinutes, v.ProductsDiscussed, v.SamplesGiven, v.MaterialsLeft,
                v.FeedbackNotes, v.NextVisitRecommendation, v.CheckInLatitude, v.CheckInLongitude, v.IsLocked,
                v.VisitPlanItemId, v.IsPlanned, v.LocationMismatch, v.OutsideTerritory, v.DurationTooShort))
            .ToListAsync(ct);
    }

    public async Task<int> AddVisitAsync(int representativeId, DoctorVisitSaveRequest request, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DoctorId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), request.DoctorId);
        var representative = await db.Representatives.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), representativeId);

        var validation = await visitValidation.EvaluateAsync(representative.TerritoryId, doctor.TerritoryId,
            doctor.Latitude, doctor.Longitude, request.CheckInLatitude, request.CheckInLongitude,
            request.DurationMinutes, ct);

        var visitDate = DateOnly.FromDateTime(request.VisitDateUtc);
        int? visitPlanItemId;
        bool isPlanned;

        if (request.VisitPlanItemId is { } explicitItemId)
        {
            var item = await db.VisitPlanItems.AsNoTracking().Include(i => i.VisitPlan)
                .FirstOrDefaultAsync(i => i.Id == explicitItemId && !i.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(VisitPlanItem), explicitItemId);
            if (item.DoctorId != request.DoctorId || item.VisitPlan.RepresentativeId != representativeId)
                throw new ValidationFailedException("This planned visit does not belong to the specified doctor/representative.");

            visitPlanItemId = explicitItemId;
            isPlanned = item.VisitPlan.Status == VisitPlanStatus.Approved;
        }
        else
        {
            visitPlanItemId = await db.VisitPlanItems.AsNoTracking()
                .Where(i => !i.IsDeleted && i.DoctorId == request.DoctorId && i.PlannedDate == visitDate
                    && i.VisitPlan.RepresentativeId == representativeId && i.VisitPlan.Status == VisitPlanStatus.Approved)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync(ct);
            isPlanned = visitPlanItemId.HasValue;
        }

        var visit = new DoctorVisit
        {
            DoctorId = request.DoctorId,
            RepresentativeId = representativeId,
            VisitDateUtc = request.VisitDateUtc,
            VisitPlanItemId = visitPlanItemId,
            IsPlanned = isPlanned,
            LocationMismatch = validation.LocationMismatch,
            OutsideTerritory = validation.OutsideTerritory,
            DurationTooShort = validation.DurationTooShort,
            DurationMinutes = request.DurationMinutes,
            ProductsDiscussed = request.ProductsDiscussed,
            SamplesGiven = request.SamplesGiven,
            MaterialsLeft = request.MaterialsLeft,
            FeedbackNotes = request.FeedbackNotes,
            NextVisitRecommendation = request.NextVisitRecommendation,
            CheckInLatitude = request.CheckInLatitude,
            CheckInLongitude = request.CheckInLongitude
        };
        db.DoctorVisits.Add(visit);
        await db.SaveChangesAsync(ct);
        return visit.Id;
    }

    public async Task<IReadOnlyList<DoctorFollowUpDto>> GetFollowUpsAsync(int doctorId, CancellationToken ct = default)
    {
        return await db.DoctorFollowUps.AsNoTracking()
            .Where(f => f.DoctorId == doctorId && !f.IsDeleted)
            .Include(f => f.OwnerRepresentative)
            .OrderBy(f => f.Status).ThenBy(f => f.DueDateUtc)
            .Select(f => new DoctorFollowUpDto(f.Id, f.DoctorId, f.DoctorVisitId, f.Type, f.DueDateUtc,
                f.OwnerRepresentativeId, f.OwnerRepresentative.FullName, f.Status, f.OutcomeNotes))
            .ToListAsync(ct);
    }

    public async Task<int> AddFollowUpAsync(DoctorFollowUpSaveRequest request, CancellationToken ct = default)
    {
        var doctorExists = await db.Doctors.AnyAsync(d => d.Id == request.DoctorId && !d.IsDeleted, ct);
        if (!doctorExists) throw new NotFoundException(nameof(Doctor), request.DoctorId);

        var ownerExists = await db.Representatives.AnyAsync(r => r.Id == request.OwnerRepresentativeId, ct);
        if (!ownerExists) throw new NotFoundException(nameof(Representative), request.OwnerRepresentativeId);

        var followUp = new DoctorFollowUp
        {
            DoctorId = request.DoctorId,
            DoctorVisitId = request.DoctorVisitId,
            Type = request.Type,
            DueDateUtc = request.DueDateUtc,
            OwnerRepresentativeId = request.OwnerRepresentativeId,
            Status = FollowUpStatus.Open
        };
        db.DoctorFollowUps.Add(followUp);
        await db.SaveChangesAsync(ct);
        return followUp.Id;
    }

    public async Task CloseFollowUpAsync(int followUpId, string? outcomeNotes, CancellationToken ct = default)
    {
        var followUp = await db.DoctorFollowUps.FirstOrDefaultAsync(f => f.Id == followUpId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(DoctorFollowUp), followUpId);

        followUp.Status = FollowUpStatus.Closed;
        followUp.OutcomeNotes = outcomeNotes;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Doctor doctor, DoctorSaveRequest request)
    {
        doctor.FullName = request.FullName;
        doctor.Specialty = request.Specialty;
        doctor.SubSpecialty = request.SubSpecialty;
        doctor.ClinicOrHospital = request.ClinicOrHospital;
        doctor.Address = request.Address;
        doctor.Governorate = request.Governorate;
        doctor.City = request.City;
        doctor.Phone = request.Phone;
        doctor.WhatsAppNumber = request.WhatsAppNumber;
        doctor.Email = request.Email;
        doctor.ClassificationId = request.ClassificationId;
        doctor.PreferredVisitingDays = request.PreferredVisitingDays;
        doctor.PreferredVisitingTimes = request.PreferredVisitingTimes;
        doctor.PrimaryRepresentativeId = request.PrimaryRepresentativeId;
        doctor.TerritoryId = request.TerritoryId;
        doctor.Status = request.Status;
        doctor.Latitude = request.Latitude;
        doctor.Longitude = request.Longitude;
    }

    private static DoctorDetailDto ToDetailDto(Doctor d) => new(
        d.Id, d.FullName, d.Specialty, d.SubSpecialty, d.ClinicOrHospital, d.Address, d.Governorate, d.City,
        d.Phone, d.WhatsAppNumber, d.Email, d.ClassificationId, d.Classification?.Name, d.PreferredVisitingDays,
        d.PreferredVisitingTimes, d.PrimaryRepresentativeId, d.PrimaryRepresentative?.FullName, d.TerritoryId,
        d.Territory?.Name, d.Status, d.Latitude, d.Longitude);
}

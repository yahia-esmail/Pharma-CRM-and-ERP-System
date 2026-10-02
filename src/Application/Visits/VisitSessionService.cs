using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Common;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Visits;

public interface IVisitSessionService
{
    Task<VisitCheckInResult> CheckInDoctorAsync(int representativeId, int doctorId, VisitCheckInRequest request,
        DateTime? clientSentAtUtc, CancellationToken ct = default);

    Task<VisitCheckInResult> CheckInPharmacyAsync(int representativeId, int pharmacyId, VisitCheckInRequest request,
        DateTime? clientSentAtUtc, CancellationToken ct = default);

    Task<VisitCheckOutResult> CheckOutDoctorAsync(int representativeId, int doctorId, int visitId,
        DoctorVisitCheckOutRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default);

    Task<VisitCheckOutResult> CheckOutPharmacyAsync(int representativeId, int pharmacyId, int visitId,
        PharmacyVisitCheckOutRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default);

    Task CancelDoctorVisitAsync(int representativeId, int doctorId, int visitId, CancellationToken ct = default);
    Task CancelPharmacyVisitAsync(int representativeId, int pharmacyId, int visitId, CancellationToken ct = default);
}

/// <summary>The mobile check-in → check-out flow (plan 6.1 item 6). Unlike the single-request
/// <c>AddVisitAsync</c>, the rep no longer reports the duration: the server derives it from the two
/// check-in/check-out events, each corrected for the device clock's measured offset (see IFieldVisit).
/// Both events are validated against the geofence, so leaving from somewhere else is caught too.</summary>
public class VisitSessionService(
    IAppDbContext db,
    IVisitValidationService validation,
    IOptions<VisitValidationOptions> options,
    TimeProvider time,
    IBusinessCalendar calendar) : IVisitSessionService
{
    private sealed record Customer(int Id, int? TerritoryId, double? Latitude, double? Longitude);

    public async Task<VisitCheckInResult> CheckInDoctorAsync(int representativeId, int doctorId,
        VisitCheckInRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking().Where(d => d.Id == doctorId && !d.IsDeleted)
            .Select(d => new Customer(d.Id, d.TerritoryId, d.Latitude, d.Longitude))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Doctor), doctorId);

        var visit = new DoctorVisit { DoctorId = doctorId };
        await CheckInAsync(visit, representativeId, doctor, request, clientSentAtUtc,
            planItems => planItems.Where(i => i.DoctorId == doctorId), ct);
        db.DoctorVisits.Add(visit);
        await db.SaveChangesAsync(ct);
        return ToCheckInResult(visit.Id, visit);
    }

    public async Task<VisitCheckInResult> CheckInPharmacyAsync(int representativeId, int pharmacyId,
        VisitCheckInRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.AsNoTracking().Where(p => p.Id == pharmacyId && !p.IsDeleted)
            .Select(p => new Customer(p.Id, p.TerritoryId, p.Latitude, p.Longitude))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Pharmacy), pharmacyId);

        var visit = new PharmacyVisit { PharmacyId = pharmacyId };
        await CheckInAsync(visit, representativeId, pharmacy, request, clientSentAtUtc,
            planItems => planItems.Where(i => i.PharmacyId == pharmacyId), ct);
        db.PharmacyVisits.Add(visit);
        await db.SaveChangesAsync(ct);
        return ToCheckInResult(visit.Id, visit);
    }

    public async Task<VisitCheckOutResult> CheckOutDoctorAsync(int representativeId, int doctorId, int visitId,
        DoctorVisitCheckOutRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default)
    {
        var visit = await db.DoctorVisits.Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == visitId && v.DoctorId == doctorId, ct)
            ?? throw new NotFoundException(nameof(DoctorVisit), visitId);

        CheckOut(visit, representativeId, new Customer(visit.DoctorId, visit.Doctor.TerritoryId, visit.Doctor.Latitude, visit.Doctor.Longitude),
            request.DeviceTimeUtc, request.Location, clientSentAtUtc);
        visit.ProductsDiscussed = request.ProductsDiscussed;
        visit.SamplesGiven = request.SamplesGiven;
        visit.MaterialsLeft = request.MaterialsLeft;
        visit.FeedbackNotes = request.FeedbackNotes;
        visit.NextVisitRecommendation = request.NextVisitRecommendation;
        visit.InterestLevel = request.InterestLevel;

        await db.SaveChangesAsync(ct);
        return ToCheckOutResult(visit.Id, visit);
    }

    public async Task<VisitCheckOutResult> CheckOutPharmacyAsync(int representativeId, int pharmacyId, int visitId,
        PharmacyVisitCheckOutRequest request, DateTime? clientSentAtUtc, CancellationToken ct = default)
    {
        var visit = await db.PharmacyVisits.Include(v => v.Pharmacy)
            .FirstOrDefaultAsync(v => v.Id == visitId && v.PharmacyId == pharmacyId, ct)
            ?? throw new NotFoundException(nameof(PharmacyVisit), visitId);

        CheckOut(visit, representativeId, new Customer(visit.PharmacyId, visit.Pharmacy.TerritoryId, visit.Pharmacy.Latitude, visit.Pharmacy.Longitude),
            request.DeviceTimeUtc, request.Location, clientSentAtUtc);
        visit.Purpose = request.Purpose;
        visit.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return ToCheckOutResult(visit.Id, visit);
    }

    public async Task CancelDoctorVisitAsync(int representativeId, int doctorId, int visitId, CancellationToken ct = default)
    {
        var visit = await db.DoctorVisits.FirstOrDefaultAsync(v => v.Id == visitId && v.DoctorId == doctorId, ct)
            ?? throw new NotFoundException(nameof(DoctorVisit), visitId);
        Cancel(visit, representativeId);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelPharmacyVisitAsync(int representativeId, int pharmacyId, int visitId, CancellationToken ct = default)
    {
        var visit = await db.PharmacyVisits.FirstOrDefaultAsync(v => v.Id == visitId && v.PharmacyId == pharmacyId, ct)
            ?? throw new NotFoundException(nameof(PharmacyVisit), visitId);
        Cancel(visit, representativeId);
        await db.SaveChangesAsync(ct);
    }

    // ---- Shared flow ---------------------------------------------------------------------------------

    private async Task CheckInAsync(IFieldVisit visit, int representativeId, Customer customer,
        VisitCheckInRequest request, DateTime? clientSentAtUtc,
        Func<IQueryable<VisitPlanItem>, IQueryable<VisitPlanItem>> forCustomer, CancellationToken ct)
    {
        var representative = await db.Representatives.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), representativeId);
        ValidateFix(request.Location);

        var receivedAt = Now;
        var offset = ClockOffsetSeconds(receivedAt, clientSentAtUtc);
        var checkInUtc = Corrected(request.DeviceTimeUtc, offset);
        if (checkInUtc > receivedAt.AddMinutes(1))
            throw new ValidationFailedException("Check-in time is after the request was sent — check the device clock.");

        var (planItemId, isPlanned) = await ResolvePlanItemAsync(representativeId, request.VisitPlanItemId,
            calendar.DateOf(checkInUtc), forCustomer, ct);

        visit.RepresentativeId = representativeId;
        visit.SessionStatus = VisitSessionStatus.Open;
        visit.VisitDateUtc = checkInUtc;
        visit.VisitPlanItemId = planItemId;
        visit.IsPlanned = isPlanned;
        visit.CheckInLatitude = request.Location?.Latitude;
        visit.CheckInLongitude = request.Location?.Longitude;
        visit.CheckInAccuracyMeters = request.Location?.AccuracyMeters;
        visit.CheckInDeviceTimeUtc = request.DeviceTimeUtc;
        visit.CheckInReceivedAtUtc = receivedAt;
        visit.CheckInClockOffsetSeconds = offset;
        visit.OutsideGeofenceReason = Trim(request.OutsideGeofenceReason, 500);

        visit.LocationMismatch = validation.IsLocationMismatch(customer.Latitude, customer.Longitude,
            request.Location?.Latitude, request.Location?.Longitude, request.Location?.AccuracyMeters);
        visit.OutsideTerritory = await validation.IsOutsideTerritoryAsync(representative.TerritoryId, customer.TerritoryId, ct);
        visit.DeviceClockSuspect = offset is { } o && Math.Abs(o) > options.Value.MaxDeviceClockOffsetMinutes * 60;
    }

    private void CheckOut(IFieldVisit visit, int representativeId, Customer customer, DateTime deviceTimeUtc,
        VisitFix? location, DateTime? clientSentAtUtc)
    {
        if (visit.RepresentativeId != representativeId) throw new ForbiddenAccessException();
        if (visit.SessionStatus != VisitSessionStatus.Open)
            throw new ValidationFailedException(visit.SessionStatus == VisitSessionStatus.Completed
                ? "This visit is already completed."
                : "This visit was cancelled.");
        ValidateFix(location);

        var receivedAt = Now;
        var offset = ClockOffsetSeconds(receivedAt, clientSentAtUtc);
        var checkOutUtc = Corrected(deviceTimeUtc, offset);
        var checkInUtc = visit.VisitDateUtc;

        var settings = options.Value;
        var clockChanged = offset is { } outOffset && visit.CheckInClockOffsetSeconds is { } inOffset
            && Math.Abs(outOffset - inOffset) > settings.MaxClockDriftDuringVisitSeconds;
        var backwards = checkOutUtc < checkInUtc;
        var minutes = backwards ? 0 : (int)Math.Round((checkOutUtc - checkInUtc).TotalMinutes);

        visit.SessionStatus = VisitSessionStatus.Completed;
        visit.DurationMinutes = minutes;
        visit.CheckOutLatitude = location?.Latitude;
        visit.CheckOutLongitude = location?.Longitude;
        visit.CheckOutAccuracyMeters = location?.AccuracyMeters;
        visit.CheckOutDeviceTimeUtc = deviceTimeUtc;
        visit.CheckOutReceivedAtUtc = receivedAt;
        visit.CheckOutClockOffsetSeconds = offset;

        // A visit that started inside the fence but "ended" far away is as suspicious as the reverse.
        visit.LocationMismatch |= validation.IsLocationMismatch(customer.Latitude, customer.Longitude,
            location?.Latitude, location?.Longitude, location?.AccuracyMeters);
        visit.DurationTooShort = validation.IsDurationTooShort(minutes);
        visit.DeviceClockSuspect |= clockChanged || backwards
            || (offset is { } o && Math.Abs(o) > settings.MaxDeviceClockOffsetMinutes * 60);
    }

    private static void Cancel(IFieldVisit visit, int representativeId)
    {
        if (visit.RepresentativeId != representativeId) throw new ForbiddenAccessException();
        if (visit.SessionStatus != VisitSessionStatus.Open)
            throw new ValidationFailedException("Only a visit in progress can be cancelled.");
        visit.SessionStatus = VisitSessionStatus.Cancelled;
        visit.IsDeleted = true;
    }

    private async Task<(int? ItemId, bool IsPlanned)> ResolvePlanItemAsync(int representativeId, int? explicitItemId,
        DateOnly visitDate, Func<IQueryable<VisitPlanItem>, IQueryable<VisitPlanItem>> forCustomer, CancellationToken ct)
    {
        if (explicitItemId is { } itemId)
        {
            var item = await forCustomer(db.VisitPlanItems.AsNoTracking().Include(i => i.VisitPlan))
                .FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted, ct);
            if (item is null || item.VisitPlan.RepresentativeId != representativeId)
                throw new ValidationFailedException("This planned visit does not belong to this customer/representative.");
            return (itemId, item.VisitPlan.Status == VisitPlanStatus.Approved);
        }

        // Same inference as the single-request AddVisitAsync: an Approved plan stop for this customer today.
        var inferred = await forCustomer(db.VisitPlanItems.AsNoTracking())
            .Where(i => !i.IsDeleted && i.PlannedDate == visitDate
                && i.VisitPlan.RepresentativeId == representativeId && i.VisitPlan.Status == VisitPlanStatus.Approved)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(ct);
        return (inferred, inferred.HasValue);
    }

    private static void ValidateFix(VisitFix? fix)
    {
        if (fix is null) return;
        if (double.IsNaN(fix.Latitude) || fix.Latitude is < -90 or > 90 || double.IsNaN(fix.Longitude) || fix.Longitude is < -180 or > 180)
            throw new ValidationFailedException("Location coordinates are out of range.");
        if (fix.AccuracyMeters is < 0)
            throw new ValidationFailedException("Location accuracy cannot be negative.");
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>Server minus device clock, from the device's own send time (X-Client-Sent-At). Null when the
    /// client didn't send it (older clients): device times are then taken at face value.</summary>
    private static double? ClockOffsetSeconds(DateTime receivedAtUtc, DateTime? clientSentAtUtc) =>
        clientSentAtUtc is { } sent ? Math.Round((receivedAtUtc - DateTime.SpecifyKind(sent, DateTimeKind.Utc)).TotalSeconds, 1) : null;

    private static DateTime Corrected(DateTime deviceTimeUtc, double? offsetSeconds) =>
        DateTime.SpecifyKind(deviceTimeUtc, DateTimeKind.Utc).AddSeconds(offsetSeconds ?? 0);

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var t && t.Length > max ? t[..max] : value.Trim();

    private static VisitCheckInResult ToCheckInResult(int id, IFieldVisit v) =>
        new(id, v.VisitDateUtc, v.IsPlanned, v.VisitPlanItemId, v.LocationMismatch, v.OutsideTerritory, v.DeviceClockSuspect);

    private static VisitCheckOutResult ToCheckOutResult(int id, IFieldVisit v) =>
        new(id, v.VisitDateUtc, v.DurationMinutes ?? 0, v.LocationMismatch, v.OutsideTerritory, v.DurationTooShort, v.DeviceClockSuspect);
}

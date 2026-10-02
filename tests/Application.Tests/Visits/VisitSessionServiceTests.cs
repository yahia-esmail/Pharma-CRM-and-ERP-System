using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Common;
using PharmaERP.Application.Visits;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Visits;

public class VisitSessionServiceTests : IDisposable
{
    private const int RepId = 7;
    private static readonly DateTime Morning = new(2026, 9, 29, 7, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db;
    private readonly FakeTimeProvider _serverClock = new(new DateTimeOffset(Morning));
    private readonly VisitSessionService _service;
    private Doctor _doctor = null!;
    private Pharmacy _pharmacy = null!;
    private VisitPlanItem _planItem = null!;

    public VisitSessionServiceTests()
    {
        _db = TestDb.Create();
        var options = Options.Create(new VisitValidationOptions { GeofenceRadiusMeters = 200, MinimumVisitDurationMinutes = 2 });
        // Territories are left unset in these tests, so the territory service is never consulted.
        _service = new VisitSessionService(_db, new VisitValidationService(null!, options), options, _serverClock,
            TestCalendar.Cairo(_serverClock));
        Seed();
    }

    private void Seed()
    {
        var rep = new Representative { Id = RepId, EmployeeCode = "REP-7", FullName = "Ahmed", CreatedByUserId = "t" };
        _doctor = new Doctor { FullName = "Dr. Samy", Specialty = "Cardiology", Latitude = 30.0444, Longitude = 31.2357, CreatedByUserId = "t" };
        _pharmacy = new Pharmacy { Name = "El-Ezaby", Latitude = 30.05, Longitude = 31.24, CreatedByUserId = "t" };
        var plan = new VisitPlan { RepresentativeId = RepId, Status = VisitPlanStatus.Approved, StartDate = DateOnly.FromDateTime(Morning), EndDate = DateOnly.FromDateTime(Morning), CreatedByUserId = "t" };
        _planItem = new VisitPlanItem { VisitPlan = plan, Doctor = _doctor, PlannedDate = DateOnly.FromDateTime(Morning), Sequence = 1, CreatedByUserId = "t" };
        _db.AddRange(rep, _doctor, _pharmacy, plan, _planItem);
        _db.SaveChanges();
    }

    private static VisitFix At(double northMeters, double accuracy = 10) =>
        new() { Latitude = 30.0444 + northMeters / 111_195.0, Longitude = 31.2357, AccuracyMeters = accuracy };

    private Task<VisitCheckInResult> CheckInAsync(DateTime deviceTime, DateTime? sentAt, VisitFix? location = null, int? planItemId = null) =>
        _service.CheckInDoctorAsync(RepId, _doctor.Id,
            new VisitCheckInRequest { DeviceTimeUtc = deviceTime, Location = location ?? At(20), VisitPlanItemId = planItemId },
            sentAt);

    private Task<VisitCheckOutResult> CheckOutAsync(int visitId, DateTime deviceTime, DateTime? sentAt, VisitFix? location = null) =>
        _service.CheckOutDoctorAsync(RepId, _doctor.Id, visitId,
            new DoctorVisitCheckOutRequest { DeviceTimeUtc = deviceTime, Location = location ?? At(25), FeedbackNotes = "Interested", InterestLevel = VisitInterestLevel.High },
            sentAt);

    [Fact]
    public async Task Online_visit_is_timed_from_the_two_events_and_linked_to_the_plan()
    {
        var checkIn = await CheckInAsync(Morning, sentAt: Morning);
        Assert.True(checkIn.IsPlanned);
        Assert.Equal(_planItem.Id, checkIn.VisitPlanItemId);
        Assert.Equal(VisitSessionStatus.Open, (await _db.DoctorVisits.SingleAsync()).SessionStatus);

        _serverClock.Advance(TimeSpan.FromMinutes(14));
        var checkOut = await CheckOutAsync(checkIn.Id, Morning.AddMinutes(14), sentAt: Morning.AddMinutes(14));

        Assert.Equal(14, checkOut.DurationMinutes);
        Assert.False(checkOut.DurationTooShort || checkOut.LocationMismatch || checkOut.DeviceClockSuspect);
        var visit = await _db.DoctorVisits.SingleAsync();
        Assert.Equal(VisitSessionStatus.Completed, visit.SessionStatus);
        Assert.Equal(VisitInterestLevel.High, visit.InterestLevel);
        Assert.Equal("Interested", visit.FeedbackNotes);
    }

    [Fact]
    public async Task Visit_recorded_offline_and_uploaded_hours_later_keeps_its_real_times()
    {
        // The rep visited 07:00–07:12 with no signal; both requests reach the server together at 11:00.
        _serverClock.SetUtcNow(new DateTimeOffset(Morning.AddHours(4)));
        var sentAt = Morning.AddHours(4);

        var checkIn = await CheckInAsync(Morning, sentAt);
        var checkOut = await CheckOutAsync(checkIn.Id, Morning.AddMinutes(12), sentAt.AddSeconds(1));

        Assert.Equal(Morning, checkIn.CheckInUtc);
        Assert.Equal(12, checkOut.DurationMinutes);
        Assert.False(checkOut.DurationTooShort);
        Assert.False(checkOut.DeviceClockSuspect);
    }

    [Fact]
    public async Task A_device_clock_that_is_off_is_corrected_and_flagged()
    {
        // Device clock 20 minutes behind the server.
        var skew = TimeSpan.FromMinutes(-20);
        var checkIn = await CheckInAsync(Morning + skew, Morning + skew);
        _serverClock.Advance(TimeSpan.FromMinutes(10));
        var checkOut = await CheckOutAsync(checkIn.Id, Morning.AddMinutes(10) + skew, Morning.AddMinutes(10) + skew);

        Assert.Equal(Morning, checkIn.CheckInUtc);   // corrected to server time
        Assert.Equal(10, checkOut.DurationMinutes);
        Assert.True(checkOut.DeviceClockSuspect);
    }

    [Fact]
    public async Task Changing_the_device_clock_during_the_visit_is_detected()
    {
        var checkIn = await CheckInAsync(Morning, Morning);
        _serverClock.Advance(TimeSpan.FromMinutes(1));
        // The rep sets the phone clock 30 minutes ahead before checking out, to fake a long visit.
        var fakeNow = Morning.AddMinutes(31);
        var checkOut = await CheckOutAsync(checkIn.Id, fakeNow, fakeNow);

        Assert.Equal(1, checkOut.DurationMinutes);   // offset correction undoes the fake half hour
        Assert.True(checkOut.DeviceClockSuspect);
        Assert.True(checkOut.DurationTooShort);
    }

    [Fact]
    public async Task Leaving_from_somewhere_else_flags_a_location_mismatch()
    {
        var checkIn = await CheckInAsync(Morning, Morning, At(30));
        Assert.False(checkIn.LocationMismatch);

        _serverClock.Advance(TimeSpan.FromMinutes(10));
        var checkOut = await CheckOutAsync(checkIn.Id, Morning.AddMinutes(10), Morning.AddMinutes(10), At(3000));

        Assert.True(checkOut.LocationMismatch);
    }

    [Fact]
    public async Task Fix_accuracy_is_credited_but_capped()
    {
        // 240 m away ±50 m: could be inside the 200 m fence → not flagged.
        Assert.False((await CheckInAsync(Morning, Morning, At(240, accuracy: 50))).LocationMismatch);

        // 900 m away "±1500 m" (cell tower): credit is capped at 100 m → flagged.
        Assert.True((await CheckInAsync(Morning, Morning, At(900, accuracy: 1500))).LocationMismatch);
    }

    [Fact]
    public async Task Check_in_without_location_is_accepted_and_the_outside_reason_is_kept()
    {
        var result = await _service.CheckInPharmacyAsync(RepId, _pharmacy.Id,
            new VisitCheckInRequest { DeviceTimeUtc = Morning, OutsideGeofenceReason = "  GPS issue inside the mall  " }, Morning);

        var visit = await _db.PharmacyVisits.SingleAsync(v => v.Id == result.Id);
        Assert.Null(visit.CheckInLatitude);
        Assert.False(result.LocationMismatch);
        Assert.Equal("GPS issue inside the mall", visit.OutsideGeofenceReason);
    }

    [Fact]
    public async Task Pharmacy_check_out_records_purpose_and_notes()
    {
        var checkIn = await _service.CheckInPharmacyAsync(RepId, _pharmacy.Id, new VisitCheckInRequest { DeviceTimeUtc = Morning }, Morning);
        _serverClock.Advance(TimeSpan.FromMinutes(6));
        await _service.CheckOutPharmacyAsync(RepId, _pharmacy.Id, checkIn.Id, new PharmacyVisitCheckOutRequest
        {
            DeviceTimeUtc = Morning.AddMinutes(6), Purpose = PharmacyVisitPurpose.Collection, Notes = "Cheque collected"
        }, Morning.AddMinutes(6));

        var visit = await _db.PharmacyVisits.SingleAsync();
        Assert.Equal((PharmacyVisitPurpose.Collection, "Cheque collected", 6), (visit.Purpose, visit.Notes, visit.DurationMinutes));
    }

    [Fact]
    public async Task A_completed_visit_cannot_be_checked_out_twice()
    {
        var checkIn = await CheckInAsync(Morning, Morning);
        await CheckOutAsync(checkIn.Id, Morning.AddMinutes(5), Morning);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => CheckOutAsync(checkIn.Id, Morning.AddMinutes(6), Morning));
        Assert.Contains("already completed", ex.Message);
    }

    [Fact]
    public async Task Another_rep_cannot_check_out_or_cancel_the_visit()
    {
        var checkIn = await CheckInAsync(Morning, Morning);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.CheckOutDoctorAsync(RepId + 1, _doctor.Id, checkIn.Id,
            new DoctorVisitCheckOutRequest { DeviceTimeUtc = Morning }, Morning));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.CancelDoctorVisitAsync(RepId + 1, _doctor.Id, checkIn.Id));
    }

    [Fact]
    public async Task Cancelled_visit_disappears_and_cannot_be_completed()
    {
        var checkIn = await CheckInAsync(Morning, Morning);

        await _service.CancelDoctorVisitAsync(RepId, _doctor.Id, checkIn.Id);

        Assert.Empty(await _db.DoctorVisits.ToListAsync());   // soft-deleted → filtered out
        await Assert.ThrowsAsync<NotFoundException>(() => CheckOutAsync(checkIn.Id, Morning.AddMinutes(5), Morning));
    }

    [Fact]
    public async Task A_plan_stop_for_another_customer_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _service.CheckInPharmacyAsync(RepId, _pharmacy.Id,
            new VisitCheckInRequest { DeviceTimeUtc = Morning, VisitPlanItemId = _planItem.Id }, Morning));
        Assert.Contains("does not belong", ex.Message);
    }

    [Fact]
    public async Task A_check_in_time_after_the_send_time_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => CheckInAsync(Morning.AddMinutes(30), Morning));
    }

    [Fact]
    public async Task Visit_after_midnight_in_cairo_counts_for_the_local_day_not_the_utc_one()
    {
        // 22:30 UTC on the 29th is 01:30 on the 30th in Cairo: the stop planned for the 30th is the one visited.
        var lateNight = new DateTime(2026, 9, 29, 22, 30, 0, DateTimeKind.Utc);
        var plan = await _db.VisitPlans.SingleAsync();
        var tomorrowItem = new VisitPlanItem { VisitPlanId = plan.Id, DoctorId = _doctor.Id, PlannedDate = new DateOnly(2026, 9, 30), Sequence = 1, CreatedByUserId = "t" };
        _db.VisitPlanItems.Add(tomorrowItem);
        await _db.SaveChangesAsync();
        _serverClock.SetUtcNow(lateNight);

        var checkIn = await CheckInAsync(lateNight, sentAt: lateNight);

        Assert.True(checkIn.IsPlanned);
        Assert.Equal(tomorrowItem.Id, checkIn.VisitPlanItemId);
    }

    public void Dispose() => _db.Dispose();
}

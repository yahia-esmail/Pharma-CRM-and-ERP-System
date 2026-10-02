using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Pilot;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Pilot;

public class PilotReportTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly PilotReportService _report;
    private Doctor _doctor = null!;
    private Pharmacy _pharmacy = null!;

    public PilotReportTests()
    {
        var clock = new FakeTimeProvider(Now);
        _report = new PilotReportService(_db, TestCalendar.Cairo(clock), clock);
        _doctor = new Doctor { FullName = "Dr. Karim", Specialty = "Cardiology", CreatedByUserId = "t" };
        _pharmacy = new Pharmacy { Name = "Tahrir", CreatedByUserId = "t" };
        _db.AddRange(_doctor, _pharmacy,
            new Representative { Id = 5, EmployeeCode = "R5", FullName = "Yahia", CreatedByUserId = "t" },
            new Representative { Id = 6, EmployeeCode = "R6", FullName = "Sara", CreatedByUserId = "t" },
            new Representative { Id = 7, EmployeeCode = "R7", FullName = "Idle", CreatedByUserId = "t" });
        _db.SaveChanges();
    }

    private void Visit(int rep, DateTime atUtc, double? accuracy, int? fixMs, bool mismatch = false, string? reason = null,
        VisitSessionStatus status = VisitSessionStatus.Completed, int? duration = 12, bool pharmacy = false)
    {
        if (pharmacy)
            _db.Add(new PharmacyVisit { PharmacyId = _pharmacy.Id, RepresentativeId = rep, VisitDateUtc = atUtc, SessionStatus = status,
                CheckInLatitude = accuracy is null ? null : 30.04, CheckInAccuracyMeters = accuracy, CheckInFixElapsedMs = fixMs,
                LocationMismatch = mismatch, OutsideGeofenceReason = reason, DurationMinutes = duration, CreatedByUserId = "t" });
        else
            _db.Add(new DoctorVisit { DoctorId = _doctor.Id, RepresentativeId = rep, VisitDateUtc = atUtc, SessionStatus = status,
                CheckInLatitude = accuracy is null ? null : 30.04, CheckInAccuracyMeters = accuracy, CheckInFixElapsedMs = fixMs,
                LocationMismatch = mismatch, OutsideGeofenceReason = reason, DurationMinutes = duration, CreatedByUserId = "t" });
    }

    private static DateTime At(int day, int hourUtc) => new(2026, 10, day, hourUtc, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Accuracy_and_fix_time_are_measured_against_the_plan_targets()
    {
        // Yahia: 10 check-ins — 9 within 30 m, one at 80 m; fixes 3–9 s plus one of 25 s.
        for (var i = 0; i < 9; i++) Visit(5, At(5, 8 + i % 8), accuracy: 8 + i, fixMs: 3000 + i * 700);
        Visit(5, At(6, 9), accuracy: 80, fixMs: 25_000, mismatch: true, reason: "GPS inaccurate here (indoors, mall, basement)", pharmacy: true);
        await _db.SaveChangesAsync();

        var report = await _report.GetAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), null);

        var yahia = Assert.Single(report.Rows);
        Assert.Equal((10, 2), (yahia.Visits, yahia.ActiveDays));
        Assert.Equal(90, yahia.Within30mPct);
        Assert.Equal(90, yahia.Within10sPct);
        Assert.Equal(12, yahia.MedianAccuracyMeters);   // nearest-rank median of 8…16, 80
        Assert.Equal(16, yahia.P90AccuracyMeters);
        Assert.Equal(10, yahia.MismatchPct);
        Assert.Equal(1, yahia.OutsideGeofenceReasons);
        Assert.Equal("GPS inaccurate here (indoors, mall, basement)", Assert.Single(report.TopOutsideReasons).Reason);
    }

    [Fact]
    public async Task Cancelled_visits_do_not_count_and_forgotten_open_ones_are_flagged()
    {
        Visit(6, At(6, 9), accuracy: 10, fixMs: 4000, status: VisitSessionStatus.Cancelled);
        Visit(6, At(6, 10), accuracy: 10, fixMs: 4000, status: VisitSessionStatus.Open, duration: null);   // two days ago, never closed
        Visit(6, At(6, 11), accuracy: null, fixMs: null);                                                  // checked in without GPS
        await _db.SaveChangesAsync();

        var sara = Assert.Single((await _report.GetAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8), null)).Rows);

        Assert.Equal(2, sara.Visits);
        Assert.Equal(1, sara.LeftOpen);
        Assert.Equal(50, sara.WithLocationPct);
        Assert.Equal(12, sara.AverageDurationMinutes);   // only completed visits have a duration
    }

    [Fact]
    public async Task Days_are_cairo_days_and_reps_without_activity_are_left_out()
    {
        Visit(5, At(7, 22), accuracy: 10, fixMs: 3000);   // 01:00 on 8 Oct in Cairo — outside a range ending 7 Oct
        Visit(5, At(7, 20), accuracy: 10, fixMs: 3000);   // 23:00 on 7 Oct in Cairo — inside
        Visit(5, At(1, 0), accuracy: 10, fixMs: 3000, reason: "Other: parking");
        await _db.SaveChangesAsync();

        var report = await _report.GetAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), null);

        Assert.Equal(2, Assert.Single(report.Rows).Visits);
        Assert.DoesNotContain(report.Rows, r => r.Name == "Idle");
        Assert.Equal("Other", Assert.Single(report.TopOutsideReasons).Reason);
        Assert.Equal(2, report.Total.Visits);
    }

    public void Dispose() => _db.Dispose();
}

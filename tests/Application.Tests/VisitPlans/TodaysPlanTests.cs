using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.VisitPlans;

public class TodaysPlanTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private readonly ApplicationDbContext _db;
    private readonly VisitPlanService _service;

    public TodaysPlanTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        // The current-user service isn't used by GetTodaysPlanAsync.
        _service = new VisitPlanService(_db, null!, TestCalendar.Cairo());
    }

    private async Task<(Doctor Doctor, Pharmacy Pharmacy, VisitPlanItem DoctorItem, VisitPlanItem PharmacyItem)> SeedAsync()
    {
        var classification = new DoctorClassification { Name = "A", CreatedByUserId = "t" };
        var doctor = new Doctor
        {
            FullName = "Dr. Samy", Specialty = "Cardiology", Address = "12 Tahrir St", City = "Cairo",
            Classification = classification, Latitude = 30.04, Longitude = 31.23, CreatedByUserId = "t"
        };
        var pharmacy = new Pharmacy { Name = "El-Ezaby", Segment = "A", City = "Giza", Latitude = 30.01, Longitude = 31.2, CreatedByUserId = "t" };
        var plan = new VisitPlan { RepresentativeId = 7, Status = VisitPlanStatus.Approved, StartDate = Today, EndDate = Today, CreatedByUserId = "t" };
        var doctorItem = new VisitPlanItem { VisitPlan = plan, Doctor = doctor, PlannedDate = Today, Sequence = 1, CreatedByUserId = "t" };
        var pharmacyItem = new VisitPlanItem { VisitPlan = plan, Pharmacy = pharmacy, PlannedDate = Today, Sequence = 2, CreatedByUserId = "t" };
        _db.AddRange(doctor, pharmacy, plan, doctorItem, pharmacyItem);
        await _db.SaveChangesAsync();
        return (doctor, pharmacy, doctorItem, pharmacyItem);
    }

    [Fact]
    public async Task Stops_carry_location_and_card_details()
    {
        await SeedAsync();

        var items = await _service.GetTodaysPlanAsync(7, Today);

        var doctor = items[0];
        Assert.Equal((30.04, 31.23), (doctor.Latitude, doctor.Longitude));
        Assert.Equal(("Cardiology", "A", "12 Tahrir St", "Cairo"), (doctor.Specialty, doctor.ClassificationName, doctor.Address, doctor.City));
        var pharmacy = items[1];
        Assert.Equal((30.01, 31.2), (pharmacy.Latitude, pharmacy.Longitude));
        Assert.Equal(("A", null, "Giza"), (pharmacy.Specialty, pharmacy.ClassificationName, pharmacy.City));
    }

    [Fact]
    public async Task Visited_at_comes_from_the_earliest_visit_linked_to_the_stop()
    {
        var (doctor, pharmacy, doctorItem, pharmacyItem) = await SeedAsync();
        var morning = new DateTime(2026, 9, 29, 7, 30, 0, DateTimeKind.Utc);
        _db.DoctorVisits.AddRange(
            new DoctorVisit { Doctor = doctor, RepresentativeId = 7, VisitDateUtc = morning.AddHours(2), VisitPlanItemId = doctorItem.Id, CreatedByUserId = "t" },
            new DoctorVisit { Doctor = doctor, RepresentativeId = 7, VisitDateUtc = morning, VisitPlanItemId = doctorItem.Id, CreatedByUserId = "t" });
        await _db.SaveChangesAsync();

        var items = await _service.GetTodaysPlanAsync(7, Today);

        Assert.Equal(morning, items.Single(i => i.Id == doctorItem.Id).VisitedAtUtc);
        Assert.Null(items.Single(i => i.Id == pharmacyItem.Id).VisitedAtUtc);
    }

    [Fact]
    public async Task Pharmacy_stops_are_marked_visited_too()
    {
        var (_, pharmacy, _, pharmacyItem) = await SeedAsync();
        var at = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
        _db.PharmacyVisits.Add(new PharmacyVisit { Pharmacy = pharmacy, RepresentativeId = 7, VisitDateUtc = at, VisitPlanItemId = pharmacyItem.Id, CreatedByUserId = "t" });
        await _db.SaveChangesAsync();

        var items = await _service.GetTodaysPlanAsync(7, Today);

        Assert.Equal(at, items.Single(i => i.Id == pharmacyItem.Id).VisitedAtUtc);
    }

    public void Dispose() => _db.Dispose();
}

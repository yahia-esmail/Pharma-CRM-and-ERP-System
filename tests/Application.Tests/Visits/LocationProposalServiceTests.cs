using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Common;
using PharmaERP.Application.Visits;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Visits;

public class LocationProposalServiceTests : IDisposable
{
    private const int RepId = 7;
    private readonly ApplicationDbContext _db;
    private readonly LocationProposalService _service;
    private readonly Doctor _unmapped;
    private readonly Pharmacy _mapped;

    public LocationProposalServiceTests()
    {
        _db = TestDb.Create();
        _service = new LocationProposalService(_db, Options.Create(new VisitValidationOptions { AutoApplyLocationMaxAccuracyMeters = 30 }),
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        _unmapped = new Doctor { FullName = "Dr. No Location", Specialty = "ENT", CreatedByUserId = "t" };
        _mapped = new Pharmacy { Name = "Mapped Pharmacy", Latitude = 30.05, Longitude = 31.24, CreatedByUserId = "t" };
        _db.AddRange(new Representative { Id = RepId, EmployeeCode = "REP-7", FullName = "Ahmed", CreatedByUserId = "t" }, _unmapped, _mapped);
        _db.SaveChanges();
    }

    private static LocationProposalRequest Fix(double accuracy, double lat = 30.0444, double lon = 31.2357) =>
        new() { Latitude = lat, Longitude = lon, AccuracyMeters = accuracy };

    [Fact]
    public async Task First_accurate_location_is_applied_immediately()
    {
        var result = await _service.ProposeForDoctorAsync(RepId, _unmapped.Id, Fix(accuracy: 12));

        Assert.Equal(LocationProposalStatus.AutoApplied, result.Status);
        var doctor = await _db.Doctors.SingleAsync(d => d.Id == _unmapped.Id);
        Assert.Equal((30.0444, 31.2357), (doctor.Latitude!.Value, doctor.Longitude!.Value));
    }

    [Fact]
    public async Task First_location_with_poor_accuracy_waits_for_review()
    {
        var result = await _service.ProposeForDoctorAsync(RepId, _unmapped.Id, Fix(accuracy: 80));

        Assert.Equal(LocationProposalStatus.Pending, result.Status);
        Assert.Null((await _db.Doctors.SingleAsync(d => d.Id == _unmapped.Id)).Latitude);
    }

    [Fact]
    public async Task Moving_an_existing_location_always_needs_a_manager_even_with_a_perfect_fix()
    {
        var result = await _service.ProposeForPharmacyAsync(RepId, _mapped.Id, Fix(accuracy: 3, lat: 30.06, lon: 31.24));

        Assert.Equal(LocationProposalStatus.Pending, result.Status);
        Assert.Equal(30.05, (await _db.Pharmacies.SingleAsync()).Latitude);

        var pending = Assert.Single(await _service.GetAsync(LocationProposalStatus.Pending));
        Assert.Equal(("Pharmacy", "Mapped Pharmacy", "Ahmed"), (pending.CustomerKind, pending.CustomerName, pending.RepresentativeName));
        Assert.InRange(pending.MoveDistanceMeters!.Value, 1100, 1125);   // 0.01° of latitude ≈ 1.1 km
    }

    [Fact]
    public async Task Approving_applies_the_location_and_rejecting_does_not()
    {
        var approved = await _service.ProposeForPharmacyAsync(RepId, _mapped.Id, Fix(5, lat: 30.051));
        var rejected = await _service.ProposeForPharmacyAsync(RepId, _mapped.Id, Fix(5, lat: 30.2));

        await _service.RejectAsync(rejected.Id, "manager-1", "Too far from the known address");
        Assert.Equal(30.05, (await _db.Pharmacies.SingleAsync()).Latitude);

        await _service.ApproveAsync(approved.Id, "manager-1", null);
        Assert.Equal(30.051, (await _db.Pharmacies.SingleAsync()).Latitude);

        var reviewed = await _db.CustomerLocationProposals.SingleAsync(p => p.Id == rejected.Id);
        Assert.Equal((LocationProposalStatus.Rejected, "manager-1", "Too far from the known address"),
            (reviewed.Status, reviewed.ReviewedByUserId, reviewed.ReviewNote));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.ApproveAsync(rejected.Id, "manager-1", null));
    }

    [Fact]
    public async Task Implausible_fixes_are_rejected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.ProposeForDoctorAsync(RepId, _unmapped.Id, Fix(accuracy: 5000)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.ProposeForDoctorAsync(RepId, _unmapped.Id, Fix(5, lat: 95)));
    }

    public void Dispose() => _db.Dispose();
}

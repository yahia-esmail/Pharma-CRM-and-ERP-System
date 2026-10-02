using PharmaERP.Application.Doctors;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Plan;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;

namespace PharmaERP.FieldApp.UI.Tests.Plan;

public class PlanLogicTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static double LatAt(double north) => Lat + north / 111_195.0;

    private static VisitPlanItemDto DoctorStop(int id, int seq, double? north = null, DateTime? visitedAt = null) =>
        new(id, DoctorId: 100 + id, DoctorName: $"Dr. {id}", PharmacyId: null, PharmacyName: null, Today, seq, null,
            north is { } n ? LatAt(n) : null, north is null ? null : Lon, "Cardiology", "A", null, "Cairo", visitedAt);

    private static VisitPlanItemDto PharmacyStop(int id, int seq, double? north = null) =>
        new(id, null, null, PharmacyId: 200 + id, PharmacyName: $"Pharmacy {id}", Today, seq, null,
            north is { } n ? LatAt(n) : null, north is null ? null : Lon);

    private static readonly IReadOnlyDictionary<int, DateTime> NoLocalVisits = new Dictionary<int, DateTime>();

    [Fact]
    public void Stops_follow_plan_order_and_the_first_unvisited_one_is_next()
    {
        var items = new[] { DoctorStop(3, 3), DoctorStop(1, 1, visitedAt: T0), PharmacyStop(2, 2) };

        var stops = PlanLogic.BuildStops(items, NoLocalVisits, position: null);

        Assert.Equal([1, 2, 3], stops.Select(s => s.Item.Sequence));
        Assert.Equal([StopStatus.Done, StopStatus.Next, StopStatus.Pending], stops.Select(s => s.Status));
        Assert.Equal(StopKind.Pharmacy, stops[1].Kind);
        Assert.Equal(202, stops[1].CustomerId);
        Assert.Equal("visits/pharmacy/202?planItem=2", stops[1].VisitUrl);
    }

    [Fact]
    public void A_visit_recorded_on_this_phone_marks_the_stop_done_before_it_syncs()
    {
        var items = new[] { DoctorStop(1, 1), DoctorStop(2, 2) };
        var local = new Dictionary<int, DateTime> { [1] = T0 };

        var stops = PlanLogic.BuildStops(items, local, null);

        Assert.Equal(StopStatus.Done, stops[0].Status);
        Assert.True(stops[0].VisitedLocallyOnly);
        Assert.Equal(StopStatus.Next, stops[1].Status);
    }

    [Fact]
    public void Server_visit_time_wins_over_the_local_mark()
    {
        var stops = PlanLogic.BuildStops([DoctorStop(1, 1, visitedAt: T0)], new Dictionary<int, DateTime> { [1] = T0.AddHours(1) }, null);

        Assert.Equal(T0, stops[0].VisitedAtUtc);
        Assert.False(stops[0].VisitedLocallyOnly);
    }

    [Fact]
    public void Distances_are_computed_from_the_rep_only_for_stops_with_a_location()
    {
        var stops = PlanLogic.BuildStops([DoctorStop(1, 1, north: 350), DoctorStop(2, 2)], NoLocalVisits, Fix());

        Assert.InRange(stops[0].DistanceMeters!.Value, 349, 351);
        Assert.Null(stops[1].DistanceMeters);
        Assert.False(stops[1].HasLocation);
    }

    [Fact]
    public void Route_runs_from_the_rep_through_remaining_mapped_stops_in_order()
    {
        var items = new[]
        {
            DoctorStop(1, 1, north: 500, visitedAt: T0),   // done: excluded
            DoctorStop(2, 2, north: 1000),
            DoctorStop(3, 3),                               // no location: counted, not measured
            PharmacyStop(4, 4, north: 3000)
        };
        var stops = PlanLogic.BuildStops(items, NoLocalVisits, Fix());

        var route = PlanLogic.SummarizeRoute(stops, Fix());

        Assert.Equal(3, route.RemainingStops);
        Assert.Equal(2, route.MappedStops);
        Assert.InRange(route.DistanceMeters, 2995, 3005);            // 0 → 1000 → 3000 m
        Assert.Equal(TimeSpan.FromMinutes(10), route.DriveTime);    // 3 km at 18 km/h
    }

    [Fact]
    public void Without_a_position_the_route_starts_at_the_next_stop()
    {
        var stops = PlanLogic.BuildStops([DoctorStop(1, 1, north: 1000), DoctorStop(2, 2, north: 1500)], NoLocalVisits, null);

        Assert.InRange(PlanLogic.SummarizeRoute(stops, null).DistanceMeters, 499, 501);
    }

    [Fact]
    public void Nearby_ranks_customers_with_a_location_and_flags_the_ones_already_planned()
    {
        var doctors = new[]
        {
            new DoctorListItemDto(1, "Dr. Far", "ENT", null, null, null, DoctorStatus.Active, LatAt(4000), Lon),
            new DoctorListItemDto(2, "Dr. Near", "Cardiology", null, "A", null, DoctorStatus.Active, LatAt(300), Lon),
            new DoctorListItemDto(3, "Dr. Unknown", "GP", null, null, null, DoctorStatus.Active),
            new DoctorListItemDto(4, "Dr. Too far", "GP", null, null, null, DoctorStatus.Active, LatAt(9000), Lon)
        };
        var pharmacies = new[] { new PharmacyListItemDto(7, "Corner Pharmacy", null, "B", null, 0, PharmacyStatus.Active, LatAt(120), Lon) };
        var plannedStops = PlanLogic.BuildStops([DoctorStop(id: -98, seq: 1) with { DoctorId = 2 }], NoLocalVisits, null);

        var nearby = PlanLogic.FindNearby(Fix(), doctors, pharmacies, plannedStops);

        Assert.Equal(["Corner Pharmacy", "Dr. Near", "Dr. Far"], nearby.Select(n => n.Name));
        Assert.Equal([false, true, false], nearby.Select(n => n.InTodaysPlan));
        Assert.Equal("Cardiology · A", nearby[1].Detail);
        Assert.Equal("visits/pharmacy/7", nearby[0].VisitUrl);
    }

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(135, "2 h 15 min")]
    public void Durations_read_naturally(int minutes, string expected)
    {
        Assert.Equal(expected, PlanLogic.FormatDuration(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Directions_link_uses_invariant_decimal_points()
    {
        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=30.0444,31.2357", PlanLogic.DirectionsUrl(30.0444, 31.2357));
    }
}

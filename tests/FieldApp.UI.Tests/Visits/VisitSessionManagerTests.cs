using System.Net;
using System.Text.Json;
using PharmaERP.Application.Doctors;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.VisitPlans;
using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Services.Visits;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Visits;

public class VisitSessionManagerTests
{
    private const int DoctorId = 101;
    private const int PlanItemId = 1;

    private readonly VisitTestHost _host = new();

    private static readonly VisitPlanItemDto DoctorStop =
        new(PlanItemId, DoctorId, "Dr. Hany", null, null, new DateOnly(2026, 9, 29), 1, null, Lat, Lon);

    private static readonly GeofenceResult Inside = new(GeofenceStatus.Inside, 12, 150);

    private Task<OpenVisit> CheckInDoctorAsync(GeoFix? fix = null) =>
        _host.Visits.CheckInAsync(StopKind.Doctor, DoctorId, "Dr. Hany", PlanItemId, fix ?? Fix(), Inside, null);

    private static JsonElement Json(string? body) => JsonDocument.Parse(body!).RootElement;

    [Fact]
    public async Task Check_in_queues_the_request_and_keeps_the_open_visit_across_an_app_restart()
    {
        var open = await CheckInDoctorAsync();

        var item = Assert.Single(_host.OutboxStore.Items);
        Assert.Equal($"api/v1/Doctors/{DoctorId}/visits/check-in", item.Url);
        Assert.Equal(open.VisitRef, item.ProducesRef);
        var body = Json(item.JsonBody);
        Assert.Equal(PlanItemId, body.GetProperty("visitPlanItemId").GetInt32());
        Assert.Equal(Lat, body.GetProperty("location").GetProperty("latitude").GetDouble());

        _host.Restart();
        var reopened = await _host.Visits.GetOpenAsync();

        Assert.NotNull(reopened);
        Assert.Equal(open.LocalId, reopened.LocalId);
        Assert.Equal(open.CheckInDeviceUtc, reopened.CheckInDeviceUtc);
        Assert.Equal(GeofenceStatus.Inside, reopened.CheckInGeofence);
    }

    [Fact]
    public async Task A_second_visit_cannot_start_while_one_is_open()
    {
        await CheckInDoctorAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _host.Visits.CheckInAsync(StopKind.Pharmacy, 202, "El-Ezaby", null, Fix(), Inside, null));
    }

    [Fact]
    public async Task Draft_survives_a_restart()
    {
        var open = await CheckInDoctorAsync();
        open.Notes = "Asked about the pediatric dose";
        open.Samples.Add(new SampleLine(7, "Concor 5mg", 2));
        open.InterestLevel = VisitInterestLevel.High;
        await _host.Visits.SaveDraftAsync(open);

        _host.Restart();
        var reopened = (await _host.Visits.GetOpenAsync())!;

        Assert.Equal("Asked about the pediatric dose", reopened.Notes);
        Assert.Equal(new SampleLine(7, "Concor 5mg", 2), Assert.Single(reopened.Samples));
        Assert.Equal(VisitInterestLevel.High, reopened.InterestLevel);
        Assert.NotNull(reopened.DraftSavedAtUtc);
    }

    [Fact]
    public async Task Visit_done_offline_is_delivered_later_with_the_server_visit_id_in_the_check_out()
    {
        await _host.SeedAsync(MasterDataSync.TodayPlan, DoctorStop);
        _host.Api.Offline = true;

        var open = await CheckInDoctorAsync();
        open.ProductsDiscussed.Add(7);
        open.Samples.Add(new SampleLine(7, "Concor 5mg", 2));
        open.InterestLevel = VisitInterestLevel.Medium;
        open.Notes = "  Prefers once-daily  ";
        _host.Clock.Advance(TimeSpan.FromMinutes(12));
        await _host.Visits.CompleteAsync(open, Fix(seconds: 720, source: GeoFixSource.Event),
            new Dictionary<int, string> { [7] = "Concor 5mg" });

        // Done on the plan at once, before anything reached the server.
        Assert.Null(await _host.Visits.GetOpenAsync());
        Assert.Equal(StopStatus.Done, Assert.Single(_host.Plan.Stops).Status);

        _host.Api.Respond($"api/v1/Doctors/{DoctorId}/visits/check-in", () => Status(HttpStatusCode.Created, """{"id":555}"""));
        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        var visitRequests = _host.Api.Requests.Where(r => r.Path.Contains("/visits/")).ToList();
        Assert.Equal([$"api/v1/Doctors/{DoctorId}/visits/check-in", $"api/v1/Doctors/{DoctorId}/visits/555/check-out"],
            visitRequests.Select(r => r.Path));
        var checkOut = Json(visitRequests[1].Body);
        Assert.Equal("Concor 5mg", checkOut.GetProperty("productsDiscussed").GetString());
        Assert.Equal("Concor 5mg x2", checkOut.GetProperty("samplesGiven").GetString());
        Assert.Equal("Prefers once-daily", checkOut.GetProperty("feedbackNotes").GetString());
        Assert.True(checkOut.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object);
        Assert.DoesNotContain(_host.OutboxStore.Items, i => i.Kind is "DoctorCheckIn" or "DoctorCheckOut");
    }

    [Fact]
    public async Task Completing_a_visit_right_after_a_restart_keeps_stops_marked_done_earlier_today()
    {
        var secondStop = DoctorStop with { Id = 2, DoctorId = 102, Sequence = 2 };
        await _host.SeedAsync(MasterDataSync.TodayPlan, DoctorStop, secondStop);
        await _host.Plan.MarkVisitedLocallyAsync(PlanItemId);

        _host.Restart();   // plan state not loaded yet
        var open = await _host.Visits.CheckInAsync(StopKind.Doctor, 102, "Dr. Mona", 2, Fix(), Inside, null);
        await _host.Visits.CompleteAsync(open, null, new Dictionary<int, string>());

        Assert.All(_host.Plan.Stops, s => Assert.Equal(StopStatus.Done, s.Status));
    }

    [Fact]
    public async Task Pharmacy_check_out_carries_purpose_and_notes()
    {
        var open = await _host.Visits.CheckInAsync(StopKind.Pharmacy, 202, "El-Ezaby", null, null, null, "GPS inaccurate here (indoors, mall, basement)");
        open.Purpose = PharmacyVisitPurpose.Collection;
        await _host.Visits.CompleteAsync(open, null, new Dictionary<int, string>());

        await _host.Processor.RunOnceAsync();

        var checkIn = Json(_host.Api.Requests[0].Body);
        Assert.Equal(JsonValueKind.Null, checkIn.GetProperty("location").ValueKind);
        Assert.Equal("GPS inaccurate here (indoors, mall, basement)", checkIn.GetProperty("outsideGeofenceReason").GetString());
        var checkOut = _host.Api.Requests[1];
        Assert.EndsWith("/check-out", checkOut.Path);
        Assert.Contains("Collection", checkOut.Body);
    }

    [Fact]
    public async Task Cancelling_before_the_check_in_was_sent_drops_it_without_calling_the_server()
    {
        _host.Api.Offline = true;
        var open = await CheckInDoctorAsync();

        await _host.Visits.CancelAsync(open);

        Assert.Empty(_host.OutboxStore.Items);
        Assert.Null(await _host.Visits.GetOpenAsync());
        _host.Api.Offline = false;
        await _host.Processor.RunOnceAsync();
        Assert.Empty(_host.Api.Requests);
    }

    [Fact]
    public async Task Cancelling_after_the_check_in_was_delivered_cancels_the_server_visit()
    {
        _host.Api.Respond($"api/v1/Doctors/{DoctorId}/visits/check-in", () => Status(HttpStatusCode.Created, """{"id":77}"""));
        var open = await CheckInDoctorAsync();
        await _host.Processor.RunOnceAsync();

        await _host.Visits.CancelAsync(open);
        await _host.Processor.RunOnceAsync();

        Assert.Equal($"api/v1/Doctors/{DoctorId}/visits/77/cancel", _host.Api.Requests[^1].Path);
        Assert.Null(await _host.Visits.GetOpenAsync());
    }

    [Fact]
    public async Task First_accurate_location_for_a_customer_is_applied_to_the_offline_copy_at_once()
    {
        await _host.SeedAsync(MasterDataSync.Doctors,
            new DoctorListItemDto(DoctorId, "Dr. Hany", "Cardiology", "Cairo", "A", null, DoctorStatus.Active));
        await _host.SeedAsync(MasterDataSync.TodayPlan, DoctorStop with { Latitude = null, Longitude = null });

        var applied = await _host.Visits.ProposeLocationAsync(StopKind.Doctor, DoctorId, "Dr. Hany", Fix(accuracy: 12), customerHasLocation: false);

        Assert.True(applied);
        var doctor = Assert.Single((await _host.MasterData.GetDoctorsAsync())!.Data);
        Assert.Equal(Lat, doctor.Latitude);
        Assert.Equal(Lat, Assert.Single((await _host.MasterData.GetTodayPlanAsync())!.Data).Latitude);
        Assert.Equal($"api/v1/Doctors/{DoctorId}/location-proposals", Assert.Single(_host.OutboxStore.Items).Url);
    }

    [Fact]
    public async Task Inaccurate_location_is_only_proposed_for_review()
    {
        await _host.SeedAsync(MasterDataSync.Pharmacies,
            new PharmacyListItemDto(202, "El-Ezaby", "Cairo", "A", null, 0, PharmacyStatus.Active));

        var applied = await _host.Visits.ProposeLocationAsync(StopKind.Pharmacy, 202, "El-Ezaby", Fix(accuracy: 60), customerHasLocation: false);

        Assert.False(applied);
        Assert.Null(Assert.Single((await _host.MasterData.GetPharmaciesAsync())!.Data).Latitude);
        Assert.Single(_host.OutboxStore.Items);
    }
}

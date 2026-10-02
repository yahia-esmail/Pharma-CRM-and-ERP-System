using System.Net;
using System.Text.Json;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Orders;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Tests.Visits;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Orders;

public class OrderEntryTests
{
    private const int PharmacyId = 5;
    private readonly VisitTestHost _host = new();

    private static OrderDraft Draft(int? serverId = null) => new()
    {
        ServerId = serverId,
        PharmacyId = PharmacyId,
        PharmacyName = "Tahrir Pharmacy",
        Lines =
        [
            new DraftLine(7, "Concor 5mg", 85, Quantity: 10, DiscountPercent: 5),
            new DraftLine(9, "Augmentin 1g", 95, Quantity: 2, BonusQuantity: 3)
        ]
    };

    private static JsonElement Json(string? body) => JsonDocument.Parse(body!).RootElement;

    [Fact]
    public void Totals_match_the_server_formula_and_bonus_units_are_free()
    {
        var totals = Draft().Totals;

        Assert.Equal(10 * 85 + 2 * 95, totals.Subtotal);
        Assert.Equal(10 * 85 * 0.95m + 2 * 95, totals.Net);
        Assert.Equal(totals.Subtotal - totals.Net, totals.Discount);
        Assert.Equal((12, 3), (totals.Units, totals.BonusUnits));
    }

    [Theory]
    [InlineData(18000, 15000, 1, true)]
    [InlineData(10000, 15000, 4999, false)]
    [InlineData(10000, 15000, 5001, true)]
    [InlineData(50000, 0, 1, false)]     // 0 = no limit
    public void Credit_warning_only_when_the_order_crosses_a_real_limit(decimal outstanding, decimal limit, decimal net, bool expected) =>
        Assert.Equal(expected, OrderMath.ExceedsCredit(outstanding, limit, net));

    [Fact]
    public async Task Submitting_sends_the_whole_order_as_one_request()
    {
        var draft = Draft();
        await _host.Orders.SaveLocalAsync(draft);

        await _host.Orders.SendAsync(draft, submit: true, Fix(accuracy: 12));

        var item = Assert.Single(_host.OutboxStore.Items);
        Assert.Equal(("POST", "api/v1/Orders", OrderEntryManager.OrderRef(draft.LocalId)), (item.Method, item.Url, item.ProducesRef));
        var body = Json(item.JsonBody);
        Assert.True(body.GetProperty("submit").GetBoolean());
        Assert.Equal(PharmacyId, body.GetProperty("pharmacyId").GetInt32());
        Assert.Equal(2, body.GetProperty("lines").GetArrayLength());
        Assert.Equal(5, body.GetProperty("lines")[0].GetProperty("discountPercent").GetDecimal());
        Assert.Equal(3, body.GetProperty("lines")[1].GetProperty("bonusQuantity").GetInt32());
        Assert.False(body.GetProperty("lines")[0].TryGetProperty("unitPrice", out _));   // the server prices lines
        Assert.Equal(12, body.GetProperty("location").GetProperty("accuracyMeters").GetDouble());
        Assert.Empty(await _host.Orders.GetDraftsAsync());

        var pending = Assert.Single(await _host.Orders.GetPendingAsync());
        Assert.Equal(draft.Totals.Net, pending.Net);

        await _host.Processor.RunOnceAsync();
        Assert.Empty(await _host.Orders.GetPendingAsync());   // delivered → shown by the server list from now on
    }

    [Fact]
    public async Task Saving_a_draft_sends_no_location()
    {
        await _host.Orders.SendAsync(Draft(), submit: false, Fix());

        var body = Json(Assert.Single(_host.OutboxStore.Items).JsonBody);
        Assert.False(body.GetProperty("submit").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("location").ValueKind);
    }

    [Fact]
    public async Task Editing_a_server_draft_replaces_it_with_put()
    {
        await _host.Orders.SendAsync(Draft(serverId: 41), submit: true, null);

        var item = Assert.Single(_host.OutboxStore.Items);
        Assert.Equal(("PUT", "api/v1/Orders/41", (string?)null), (item.Method, item.Url, item.ProducesRef));
    }

    [Fact]
    public async Task Empty_order_cannot_be_submitted_but_can_be_saved_as_draft()
    {
        var empty = new OrderDraft { PharmacyId = PharmacyId, PharmacyName = "Tahrir Pharmacy" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Orders.SendAsync(empty, submit: true, null));
        await _host.Orders.SendAsync(empty, submit: false, null);
        Assert.Single(_host.OutboxStore.Items);
    }

    [Fact]
    public async Task Order_taken_during_an_offline_visit_is_linked_to_the_visits_server_id()
    {
        _host.Api.Offline = true;
        var visit = await _host.Visits.CheckInAsync(StopKind.Pharmacy, PharmacyId, "Tahrir Pharmacy", null, Fix(), null, null);
        var draft = Draft();
        draft.VisitLocalId = visit.LocalId;
        await _host.Orders.SendAsync(draft, submit: true, null);

        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}/visits/check-in", () => Status(HttpStatusCode.Created, """{"id":321}"""));
        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        Assert.Equal([$"api/v1/Pharmacies/{PharmacyId}/visits/check-in", "api/v1/Orders"], _host.Api.Requests.Select(r => r.Path));
        Assert.Equal(321, Json(_host.Api.Requests[1].Body).GetProperty("pharmacyVisitId").GetInt32());
    }

    [Fact]
    public async Task Visit_cancelled_before_it_synced_is_dropped_from_the_order_instead_of_blocking_it()
    {
        _host.Api.Offline = true;
        var visit = await _host.Visits.CheckInAsync(StopKind.Pharmacy, PharmacyId, "Tahrir Pharmacy", null, Fix(), null, null);
        await _host.Visits.CancelAsync(visit);   // check-in never left the phone
        var draft = Draft();
        draft.VisitLocalId = visit.LocalId;
        await _host.Orders.SendAsync(draft, submit: true, null);

        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        var request = Assert.Single(_host.Api.Requests);
        Assert.Equal(JsonValueKind.Null, Json(request.Body).GetProperty("pharmacyVisitId").ValueKind);
        Assert.Empty(_host.OutboxStore.Items);
    }

    [Fact]
    public async Task An_order_never_sent_can_go_back_into_the_editor()
    {
        var draft = Draft();
        await _host.Orders.SendAsync(draft, submit: true, null);

        var reopened = await _host.Orders.ReopenPendingAsync(draft.LocalId);

        Assert.NotNull(reopened);
        Assert.Empty(_host.OutboxStore.Items);
        Assert.Empty(await _host.Orders.GetPendingAsync());
        Assert.Equal(draft.LocalId, Assert.Single(await _host.Orders.GetDraftsAsync()).LocalId);
    }

    [Fact]
    public async Task An_order_already_tried_is_not_reopened_because_it_may_exist_on_the_server()
    {
        var draft = Draft();
        await _host.Orders.SendAsync(draft, submit: true, null);
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.ServiceUnavailable));   // reply lost/failed
        await _host.Processor.RunOnceAsync();

        Assert.Null(await _host.Orders.ReopenPendingAsync(draft.LocalId));
        Assert.Single(_host.OutboxStore.Items);
    }

    [Fact]
    public async Task An_order_the_server_rejected_can_be_fixed_and_resent()
    {
        var draft = Draft();
        await _host.Orders.SendAsync(draft, submit: true, null);
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.BadRequest, """{"title":"Discount must be between 0% and 100%."}"""));
        await _host.Processor.RunOnceAsync();
        Assert.Equal(OutboxStatus.NeedsAttention, Assert.Single(_host.OutboxStore.Items).Status);

        Assert.NotNull(await _host.Orders.ReopenPendingAsync(draft.LocalId));
        Assert.Empty(_host.OutboxStore.Items);
    }

    [Fact]
    public async Task Drafts_survive_closing_the_app()
    {
        var draft = Draft();
        await _host.Orders.SaveLocalAsync(draft);

        _host.Restart();

        var restored = Assert.Single(await _host.Orders.GetDraftsAsync());
        Assert.Equal(draft.LocalId, restored.LocalId);
        Assert.Equal(draft.Totals, restored.Totals);
    }

    [Fact]
    public async Task Cancel_is_queued_and_shown_as_pending_until_sent()
    {
        await _host.Orders.CancelAsync(41, "Tahrir Pharmacy");

        Assert.Equal("api/v1/Orders/41/cancel", Assert.Single(_host.OutboxStore.Items).Url);
        Assert.Contains(41, await _host.Orders.GetPendingCancelsAsync());

        await _host.Processor.RunOnceAsync();
        Assert.Empty(await _host.Orders.GetPendingCancelsAsync());
    }

    [Fact]
    public async Task Order_list_and_details_still_open_offline()
    {
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.OK,
            """{"items":[{"id":41,"pharmacyId":5,"pharmacyName":"Tahrir Pharmacy","representativeId":5,"representativeName":"Yahia","orderDateUtc":"2026-10-01T09:00:00Z","status":"Rejected","totalAmount":997.5,"rejectionReason":"Over credit limit","lineCount":2}],"totalCount":1,"pageNumber":1,"pageSize":100}"""));
        _host.Api.Respond("api/v1/Orders/41", () => Status(HttpStatusCode.OK,
            """{"id":41,"pharmacyId":5,"pharmacyName":"Tahrir Pharmacy","representativeId":5,"representativeName":"Yahia","orderDateUtc":"2026-10-01T09:00:00Z","status":"Draft","rejectionReason":null,"lines":[],"totalAmount":0,"saleId":null}"""));
        var online = await _host.OrdersStore.RefreshAsync();
        await _host.OrdersStore.GetDetailAsync(41);

        _host.Api.Offline = true;
        Assert.Null(await _host.OrdersStore.RefreshAsync());
        var cached = await _host.OrdersStore.GetCachedAsync();

        Assert.Equal("Over credit limit", Assert.Single(cached!.Data).RejectionReason);
        Assert.Equal(online!.SavedAtUtc, cached.SavedAtUtc);
        Assert.Equal(41, (await _host.OrdersStore.GetDetailAsync(41))!.Id);
    }
}

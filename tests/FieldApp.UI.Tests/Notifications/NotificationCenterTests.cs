using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Notifications;
using PharmaERP.FieldApp.UI.Tests.Visits;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Notifications;

public class NotificationCenterTests
{
    private readonly VisitTestHost _host = new();
    private readonly NotificationCenter _center;

    private const string Inbox = """
        [{"id":3,"type":"OrderRejected","message":"Order #41 was rejected: Over credit limit","relatedEntityType":"Order","relatedEntityId":41,"isRead":false,"createdAtUtc":"2026-10-02T09:00:00Z"},
         {"id":2,"type":"ExpenseApproved","message":"Your expense #8 was approved.","relatedEntityType":"Expense","relatedEntityId":8,"isRead":false,"createdAtUtc":"2026-10-02T08:00:00Z"},
         {"id":1,"type":"PlannedVisitNotLogged","message":"Planned visit to Dr. Karim was not logged.","relatedEntityType":"VisitPlanItem","relatedEntityId":5,"isRead":true,"createdAtUtc":"2026-10-01T08:00:00Z"}]
        """;

    public NotificationCenterTests()
    {
        var http = new HttpClient(_host.Api, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") };
        _center = new NotificationCenter(new NotificationsApi(http), _host.Storage, _host.Outbox, _host.Clock,
            NullLogger<NotificationCenter>.Instance);
    }

    private void ServeInbox() => _host.Api.Respond("api/v1/Notifications", () => Status(HttpStatusCode.OK, Inbox));

    [Theory]
    [InlineData("OrderRejected", "Order", 41, "orders/41/edit")]
    [InlineData("ExpenseApproved", "Expense", 8, "expenses")]
    [InlineData("ReturnRejected", "ReturnTransaction", 3, "returns")]
    [InlineData("CustodyBalanceAging", "Representative", 6, "custody?tab=financial")]
    [InlineData("NearExpiry", "ProductBatch", 2, "custody")]
    [InlineData("PlannedVisitNotLogged", "VisitPlanItem", 5, "plan")]
    [InlineData("SomethingNew", null, null, "notifications")]
    public void Each_notification_opens_the_screen_of_its_record(string type, string? entity, int? id, string expected) =>
        Assert.Equal(expected, NotificationLinks.For(type, entity, id));

    [Fact]
    public async Task Reading_one_offline_drops_the_badge_at_once_and_syncs_later()
    {
        ServeInbox();
        await _center.RefreshAsync();
        Assert.Equal(2, _center.UnreadCount);

        _host.Api.Offline = true;
        await _center.MarkReadAsync(3);

        Assert.Equal(1, _center.UnreadCount);
        Assert.Equal("api/v1/Notifications/3/read", Assert.Single(_host.OutboxStore.Items).Url);
    }

    [Fact]
    public async Task A_refresh_before_the_read_synced_does_not_bring_it_back_as_unread()
    {
        ServeInbox();
        await _center.RefreshAsync();
        _host.Api.Offline = true;
        await _center.MarkReadAsync(3);

        _host.Api.Offline = false;
        ServeInbox();   // the server still says unread — the read is queued, not sent
        _host.Restart();
        var reopened = new NotificationCenter(new NotificationsApi(new HttpClient(_host.Api, false) { BaseAddress = new Uri("https://api.test/") }),
            _host.Storage, _host.Outbox, _host.Clock, NullLogger<NotificationCenter>.Instance);
        await reopened.RefreshAsync();

        Assert.True(reopened.Items.Single(n => n.Id == 3).IsRead);
        Assert.Equal(1, reopened.UnreadCount);
    }

    [Fact]
    public async Task Mark_all_read_is_one_request()
    {
        ServeInbox();
        await _center.RefreshAsync();

        await _center.MarkAllReadAsync();
        await _center.MarkAllReadAsync();   // nothing left unread: no second request

        Assert.Equal(0, _center.UnreadCount);
        Assert.Equal("api/v1/Notifications/mark-all-read", Assert.Single(_host.OutboxStore.Items).Url);
    }

    [Fact]
    public async Task List_is_kept_for_offline()
    {
        ServeInbox();
        await _center.RefreshAsync();
        _host.Api.Offline = true;

        var offline = new NotificationCenter(new NotificationsApi(new HttpClient(_host.Api, false) { BaseAddress = new Uri("https://api.test/") }),
            _host.Storage, _host.Outbox, _host.Clock, NullLogger<NotificationCenter>.Instance);
        Assert.False(await offline.RefreshAsync());

        Assert.Equal(3, offline.Items.Count);
        Assert.Equal(2, offline.UnreadCount);
    }
}

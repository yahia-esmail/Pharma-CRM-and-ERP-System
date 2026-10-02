using System.Net;
using PharmaERP.FieldApp.UI.Services.Offline;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Offline;

public class OutboxProcessorTests
{
    private readonly OutboxTestHost _host = new();

    [Fact]
    public async Task Delivered_item_is_removed_and_sent_with_its_id_as_idempotency_key()
    {
        var item = await _host.EnqueueAsync("api/v1/Expenses", new { amount = 150 });

        var result = await _host.Processor.RunOnceAsync();

        Assert.Equal(1, result.Sent);
        Assert.Empty(_host.Store.Items);
        var request = Assert.Single(_host.Api.Requests);
        Assert.Equal(item.Id.ToString(), request.IdempotencyKey);
        Assert.Equal("""{"amount":150}""", request.Body);
    }

    [Fact]
    public async Task Ten_operations_queued_offline_are_all_delivered_in_order_once_back_online()
    {
        _host.Api.Offline = true;
        for (var i = 1; i <= 10; i++)
            await _host.EnqueueAsync($"api/v1/Test/{i}");

        var offlineRun = await _host.Processor.RunOnceAsync();
        Assert.True(offlineRun.StoppedForConnectivity);
        Assert.Equal(10, _host.Store.Items.Count);

        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));   // past the first backoff step
        var onlineRun = await _host.Processor.RunOnceAsync();

        Assert.Equal(10, onlineRun.Sent);
        Assert.Empty(_host.Store.Items);
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"api/v1/Test/{i}"), _host.Api.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task Network_failure_backs_off_and_stops_the_run()
    {
        _host.Api.Offline = true;
        var first = await _host.EnqueueAsync("api/v1/A");
        await _host.EnqueueAsync("api/v1/B");

        var result = await _host.Processor.RunOnceAsync();

        Assert.True(result.StoppedForConnectivity);
        var stored = _host.Store.Items[0];
        Assert.Equal(first.Id, stored.Id);
        Assert.Equal(1, stored.Attempts);
        Assert.Equal(OutboxStatus.Pending, stored.Status);
        Assert.Equal(_host.Clock.GetUtcNow().UtcDateTime.AddSeconds(5), stored.NextAttemptAtUtc);
        Assert.Equal(0, _host.Store.Items[1].Attempts);   // the run stopped before trying B
    }

    [Fact]
    public async Task Item_is_not_resent_before_its_backoff_elapses()
    {
        _host.Api.Respond("api/v1/A", () => Status(HttpStatusCode.ServiceUnavailable));
        await _host.EnqueueAsync("api/v1/A");
        await _host.Processor.RunOnceAsync();

        var early = await _host.Processor.RunOnceAsync();
        Assert.Equal(0, early.Sent);
        Assert.Single(_host.Api.Requests);

        _host.Clock.Advance(TimeSpan.FromSeconds(6));
        var later = await _host.Processor.RunOnceAsync();
        Assert.Equal(1, later.Sent);
    }

    [Fact]
    public async Task Retry_after_server_error_reuses_the_same_idempotency_key()
    {
        _host.Api.Respond("api/v1/Collections", () => Status(HttpStatusCode.InternalServerError));
        await _host.EnqueueAsync("api/v1/Collections", new { amount = 5000 });

        await _host.Processor.RunOnceAsync();
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        Assert.Equal(2, _host.Api.Requests.Count);
        Assert.Single(_host.Api.Requests.Select(r => r.IdempotencyKey).Distinct());
        Assert.Empty(_host.Store.Items);
    }

    [Fact]
    public async Task Validation_error_needs_attention_and_is_not_retried_automatically()
    {
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.BadRequest,
            """{"title":"Credit limit exceeded","errors":{"Lines":["Pharmacy is over its credit limit."]}}"""));
        await _host.EnqueueAsync("api/v1/Orders");

        await _host.Processor.RunOnceAsync();
        _host.Clock.Advance(TimeSpan.FromHours(1));
        await _host.Processor.RunOnceAsync();

        var item = Assert.Single(_host.Store.Items);
        Assert.Equal(OutboxStatus.NeedsAttention, item.Status);
        Assert.Equal(400, item.LastStatusCode);
        Assert.Equal("Credit limit exceeded — Pharmacy is over its credit limit.", item.LastError);
        Assert.Single(_host.Api.Requests);
    }

    [Fact]
    public async Task Dependent_item_gets_the_server_id_produced_by_its_parent()
    {
        var orderRef = $"order:{Guid.NewGuid()}";
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.Created, """{"id":4321}"""));
        var order = await _host.EnqueueAsync("api/v1/Orders", new { pharmacyId = 7 }, producesRef: orderRef);
        var line = await _host.EnqueueAsync($"api/v1/Orders/{OutboxRefs.Placeholder(orderRef)}/lines",
            new { productId = 3, orderId = OutboxRefs.Placeholder(orderRef) }, dependsOn: order.Id);
        await _host.EnqueueAsync($"api/v1/Orders/{OutboxRefs.Placeholder(orderRef)}/submit", dependsOn: line.Id);

        var result = await _host.Processor.RunOnceAsync();

        Assert.Equal(3, result.Sent);
        Assert.Equal(["api/v1/Orders", "api/v1/Orders/4321/lines", "api/v1/Orders/4321/submit"],
            _host.Api.Requests.Select(r => r.Path));
        Assert.Equal("""{"productId":3,"orderId":4321}""", _host.Api.Requests[1].Body);
    }

    [Fact]
    public async Task Children_wait_while_their_parent_needs_attention_and_independent_items_still_flow()
    {
        _host.Api.Respond("api/v1/Orders", () => Status(HttpStatusCode.UnprocessableEntity, """{"title":"Pharmacy is blocked"}"""));
        var order = await _host.EnqueueAsync("api/v1/Orders", producesRef: "order:x");
        await _host.EnqueueAsync("api/v1/Orders/{ref:order:x}/lines", dependsOn: order.Id);
        await _host.EnqueueAsync("api/v1/Expenses");

        var result = await _host.Processor.RunOnceAsync();

        Assert.Equal(1, result.Sent);
        Assert.Equal(["api/v1/Orders", "api/v1/Expenses"], _host.Api.Requests.Select(r => r.Path));
        Assert.Equal(2, _host.Store.Items.Count);
    }

    [Fact]
    public async Task Conflict_while_the_first_attempt_is_still_processing_is_retried_after_retry_after()
    {
        _host.Api.Respond("api/v1/Returns", () =>
        {
            var busy = Status(HttpStatusCode.Conflict, """{"title":"still being processed"}""");
            busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
            return busy;
        });
        await _host.EnqueueAsync("api/v1/Returns");

        var first = await _host.Processor.RunOnceAsync();
        Assert.False(first.StoppedForConnectivity);
        Assert.Equal(_host.Clock.GetUtcNow().UtcDateTime.AddSeconds(2), first.NextDueUtc);
        Assert.Equal(OutboxStatus.Pending, _host.Store.Items[0].Status);

        _host.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, (await _host.Processor.RunOnceAsync()).Sent);
    }

    [Fact]
    public async Task Unauthorized_is_treated_as_transient_not_as_a_rejection()
    {
        _host.Api.Respond("api/v1/Expenses", () => Status(HttpStatusCode.Unauthorized));
        await _host.EnqueueAsync("api/v1/Expenses");

        var result = await _host.Processor.RunOnceAsync();

        Assert.True(result.StoppedForConnectivity);
        Assert.Equal(OutboxStatus.Pending, _host.Store.Items[0].Status);
    }

    [Fact]
    public async Task Missing_parent_id_needs_attention_instead_of_sending_a_placeholder()
    {
        await _host.EnqueueAsync("api/v1/Orders/{ref:order:never-created}/lines");

        await _host.Processor.RunOnceAsync();

        Assert.Empty(_host.Api.Requests);
        Assert.Equal(OutboxStatus.NeedsAttention, _host.Store.Items[0].Status);
    }
}

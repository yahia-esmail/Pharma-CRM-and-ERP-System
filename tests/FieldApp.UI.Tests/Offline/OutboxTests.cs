using PharmaERP.FieldApp.UI.Services.Offline;

namespace PharmaERP.FieldApp.UI.Tests.Offline;

public class OutboxTests
{
    private readonly OutboxTestHost _host = new();

    [Fact]
    public async Task Items_keep_insertion_order_even_within_the_same_clock_tick()
    {
        var a = await _host.EnqueueAsync("api/v1/A");
        var b = await _host.EnqueueAsync("api/v1/B");
        var c = await _host.EnqueueAsync("api/v1/C");

        Assert.Equal([a.Id, b.Id, c.Id], _host.Store.Items.Select(i => i.Id));
        Assert.True(a.Sequence < b.Sequence && b.Sequence < c.Sequence);
    }

    [Fact]
    public async Task Discard_removes_the_item_and_everything_that_depends_on_it()
    {
        var order = await _host.EnqueueAsync("api/v1/Orders", producesRef: "order:1");
        var line = await _host.EnqueueAsync("api/v1/Orders/{ref:order:1}/lines", dependsOn: order.Id);
        await _host.EnqueueAsync("api/v1/Orders/{ref:order:1}/submit", dependsOn: line.Id);
        var unrelated = await _host.EnqueueAsync("api/v1/Expenses");

        var removed = await _host.Outbox.DiscardAsync(order.Id);

        Assert.Equal(3, removed);
        Assert.Equal([unrelated.Id], _host.Store.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Retry_puts_a_rejected_item_back_in_the_queue()
    {
        var item = await _host.EnqueueAsync("api/v1/Orders");
        item.Status = OutboxStatus.NeedsAttention;
        await _host.Store.SaveAsync(item);
        var woken = false;
        _host.Outbox.WorkQueued += () => woken = true;

        await _host.Outbox.RetryAsync(item.Id);

        Assert.Equal(OutboxStatus.Pending, _host.Store.Items[0].Status);
        Assert.True(woken);
    }

    [Fact]
    public async Task Summary_counts_pending_and_rejected_separately()
    {
        await _host.EnqueueAsync("api/v1/A");
        var rejected = await _host.EnqueueAsync("api/v1/B");
        rejected.Status = OutboxStatus.NeedsAttention;
        await _host.Store.SaveAsync(rejected);

        var summary = await _host.Outbox.GetSummaryAsync();

        Assert.Equal(new OutboxSummary(1, 1), summary);
    }
}

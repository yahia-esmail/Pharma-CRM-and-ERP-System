using System.Net;
using System.Text.Json;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Collections;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Tests.Visits;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Collections;

public class CollectionEntryTests
{
    private readonly VisitTestHost _host = new();

    private static readonly string JpegBase64 = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 0xFF, 0xD9]);

    private static PharmacyLedgerLineDto Invoice(int id, int month, decimal open) =>
        new(id, new DateTime(2026, month, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, month, 30, 0, 0, 0, DateTimeKind.Utc),
            open, 0, 0, open);

    private static CollectionDraft Cheque(params CollectionPhoto[] photos) => new()
    {
        PharmacyId = 5,
        PharmacyName = "Tahrir Pharmacy",
        Amount = 2500,
        Method = PaymentMethod.Cheque,
        ReferenceNumber = " CHQ-1042 ",
        Allocations = [new InvoiceAllocation(11, 2000), new InvoiceAllocation(12, 500)],
        Photos = photos.ToList()
    };

    private static CollectionPhoto Photo(string label) => new(label, $"{label}.jpg", "image/jpeg", JpegBase64);

    private static JsonElement Json(string? body) => JsonDocument.Parse(body!).RootElement;

    [Fact]
    public void A_cheque_needs_its_number_and_cash_does_not()
    {
        var cheque = Cheque();
        cheque.ReferenceNumber = null;
        Assert.Contains(CollectionRules.Validate(cheque, null), p => p.Contains("cheque number"));

        var cash = new CollectionDraft { PharmacyId = 5, Amount = 100, Method = PaymentMethod.Cash };
        Assert.Empty(CollectionRules.Validate(cash, null));
    }

    [Fact]
    public void Split_is_checked_against_the_amount_and_what_each_invoice_still_owes()
    {
        var invoices = new[] { Invoice(11, 7, 1000), Invoice(12, 8, 2000) };
        var draft = Cheque();   // 2000 on #11, which only owes 1000

        Assert.Contains(CollectionRules.Validate(draft, invoices), p => p.Contains("#11"));

        draft.Allocations = [new InvoiceAllocation(11, 1000), new InvoiceAllocation(12, 2000)];   // 3000 > 2500
        Assert.Contains(CollectionRules.Validate(draft, invoices), p => p.Contains("more than the amount"));
    }

    [Fact]
    public void Fill_oldest_first_matches_how_the_server_applies_an_unsplit_payment()
    {
        var invoices = new[] { Invoice(12, 8, 2000), Invoice(11, 7, 1000), Invoice(13, 9, 3000) };

        var split = CollectionRules.OldestFirst(2500, invoices);

        Assert.Equal([new InvoiceAllocation(11, 1000), new InvoiceAllocation(12, 1500)], split);
    }

    [Fact]
    public void Expiry_flags_use_a_60_day_window()
    {
        var today = new DateOnly(2026, 10, 2);
        Assert.True(CustodyRules.IsNearExpiry(today.AddDays(60), today));
        Assert.False(CustodyRules.IsNearExpiry(today.AddDays(61), today));
        Assert.True(CustodyRules.IsExpired(today.AddDays(-1), today));
        Assert.False(CustodyRules.IsNearExpiry(today.AddDays(-1), today));   // expired, not "near"
    }

    [Fact]
    public async Task Collection_with_photos_recorded_offline_uploads_them_against_the_server_id_later()
    {
        _host.Api.Offline = true;
        await _host.Collections.SendAsync(Cheque(Photo("Cheque front"), Photo("Cheque back")));

        Assert.Equal(3, _host.OutboxStore.Items.Count);
        Assert.Equal(2, _host.OutboxStore.Files.Count);
        Assert.Equal(2500, await _host.Collections.GetUnsyncedTotalAsync());

        _host.Api.Respond("api/v1/Collections", () => Status(HttpStatusCode.Created, """{"id":77}"""));
        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        Assert.Equal(["api/v1/Collections", "api/v1/Collections/77/attachments", "api/v1/Collections/77/attachments"],
            _host.Api.Requests.Select(r => r.Path));
        var body = Json(_host.Api.Requests[0].Body);
        Assert.Equal("CHQ-1042", body.GetProperty("referenceNumber").GetString());
        Assert.Equal("Cheque", body.GetProperty("paymentMethod").GetString());
        Assert.Equal(2, body.GetProperty("allocations").GetArrayLength());
        Assert.Contains("filename=cheque-front-1.jpg", _host.Api.Requests[1].Body);
        Assert.Contains("Content-Type: image/jpeg", _host.Api.Requests[1].Body);
        Assert.Empty(_host.OutboxStore.Items);
        Assert.Empty(_host.OutboxStore.Files);   // photo bytes removed once uploaded
        Assert.Equal(0, await _host.Collections.GetUnsyncedTotalAsync());
    }

    [Fact]
    public async Task Photos_wait_while_their_collection_needs_attention()
    {
        await _host.Collections.SendAsync(Cheque(Photo("Cheque front")));
        _host.Api.Respond("api/v1/Collections", () => Status(HttpStatusCode.BadRequest, """{"title":"Invoice #11 only has 1,000.00 left to pay."}"""));

        await _host.Processor.RunOnceAsync();

        Assert.Single(_host.Api.Requests);   // the photo never went out
        var pending = Assert.Single(await _host.Collections.GetPendingAsync());
        Assert.Equal(OutboxStatus.NeedsAttention, _host.OutboxStore.Items.Single(i => i.Id == pending.CollectionOutboxId).Status);
    }

    [Fact]
    public async Task Discarding_a_collection_also_drops_its_photos_from_the_phone()
    {
        var pending = await _host.Collections.SendAsync(Cheque(Photo("Cheque front"), Photo("Cheque back")));

        await _host.Outbox.DiscardAsync(pending.CollectionOutboxId);

        Assert.Empty(_host.OutboxStore.Items);
        Assert.Empty(_host.OutboxStore.Files);
    }

    [Fact]
    public async Task Cash_count_is_sent_after_the_collections_queued_before_it()
    {
        await _host.Collections.SendAsync(new CollectionDraft { PharmacyId = 5, PharmacyName = "Tahrir", Amount = 300, Method = PaymentMethod.Cash });
        await _host.Collections.RequestCashCountAsync(1200, "Short 50 — change given");

        Assert.True(await _host.Collections.HasPendingCashCountAsync());
        await _host.Processor.RunOnceAsync();

        Assert.Equal(["api/v1/Collections", "api/v1/Collections/mine/reconciliations"], _host.Api.Requests.Select(r => r.Path));
        var count = Json(_host.Api.Requests[1].Body);
        Assert.Equal((1200m, "Short 50 — change given"), (count.GetProperty("countedBalance").GetDecimal(), count.GetProperty("reason").GetString()));
    }
}

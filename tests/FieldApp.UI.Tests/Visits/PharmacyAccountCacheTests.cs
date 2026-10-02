using System.Net;
using PharmaERP.Application.Pharmacies;
using PharmaERP.FieldApp.UI.Services.Visits;
using static PharmaERP.FieldApp.UI.Tests.Offline.FakeApi;

namespace PharmaERP.FieldApp.UI.Tests.Visits;

public class PharmacyAccountCacheTests
{
    private const int PharmacyId = 202;
    private readonly VisitTestHost _host = new();

    private const string LedgerJson = """
        {"pharmacyId":202,"totalSales":50000,"totalCollected":32000,"outstandingBalance":18000,
         "aging0To30":9000,"aging31To60":5000,"aging60Plus":4000,
         "lines":[{"saleId":1,"saleDateUtc":"2026-07-01T00:00:00Z","dueDateUtc":"2026-07-31T00:00:00Z","amount":4000,"daysOverdue":60},
                  {"saleId":2,"saleDateUtc":"2026-09-20T00:00:00Z","dueDateUtc":"2026-10-20T00:00:00Z","amount":9000,"daysOverdue":0}]}
        """;

    private static string DetailJson(decimal creditLimit) => $$"""
        {"id":202,"name":"El-Ezaby","paymentTermDays":30,"creditLimit":{{creditLimit}},"status":"Active"}
        """;

    private void ServeLedger() =>
        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}/ledger", () => Status(HttpStatusCode.OK, LedgerJson));

    [Fact]
    public async Task Online_snapshot_combines_ledger_and_credit_terms_and_is_saved()
    {
        ServeLedger();
        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}", () => Status(HttpStatusCode.OK, DetailJson(15000)));

        var account = (await _host.Accounts.GetAsync(PharmacyId))!.Data;

        Assert.Equal(18000, account.Ledger.OutstandingBalance);
        Assert.Equal(15000, account.CreditLimit);
        Assert.Equal(-3000, account.AvailableCredit);
        Assert.True(account.OverCreditLimit);
        Assert.Equal(1, account.OverdueInvoices);
        Assert.NotNull(await _host.Accounts.GetCachedAsync(PharmacyId));
    }

    [Fact]
    public async Task Offline_returns_the_last_saved_snapshot()
    {
        ServeLedger();
        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}", () => Status(HttpStatusCode.OK, DetailJson(40000)));
        var first = await _host.Accounts.GetAsync(PharmacyId);

        _host.Api.Offline = true;
        _host.Clock.Advance(TimeSpan.FromHours(3));
        var offline = await _host.Accounts.GetAsync(PharmacyId);

        Assert.NotNull(offline);
        Assert.Equal(first!.SavedAtUtc, offline.SavedAtUtc);
        Assert.Equal(22000, offline.Data.AvailableCredit);
    }

    [Fact]
    public async Task Never_seen_and_offline_gives_nothing()
    {
        _host.Api.Offline = true;
        Assert.Null(await _host.Accounts.GetAsync(PharmacyId));
    }

    [Fact]
    public async Task Detail_failure_keeps_previously_known_credit_terms()
    {
        ServeLedger();
        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}", () => Status(HttpStatusCode.OK, DetailJson(15000)));
        await _host.Accounts.GetAsync(PharmacyId);

        ServeLedger();
        _host.Api.Respond($"api/v1/Pharmacies/{PharmacyId}", () => Status(HttpStatusCode.InternalServerError));
        var account = (await _host.Accounts.GetAsync(PharmacyId))!.Data;

        Assert.Equal(15000, account.CreditLimit);
        Assert.Equal(30, account.PaymentTermDays);
    }

    [Fact]
    public void Zero_credit_limit_means_no_limit()
    {
        var ledger = new PharmacyLedgerDto(PharmacyId, 0, 0, 5000, 0, 0, 0, []);
        var account = new PharmacyAccount(ledger, 0, 30);

        Assert.Null(account.AvailableCredit);
        Assert.False(account.OverCreditLimit);
    }
}

using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Custody;
using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Collections;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Requests;
using PharmaERP.FieldApp.UI.Tests.Offline;
using PharmaERP.FieldApp.UI.Tests.Visits;

namespace PharmaERP.FieldApp.UI.Tests.Requests;

public class RequestEntryTests
{
    private static readonly DateOnly Today = new(2026, 10, 2);
    private readonly VisitTestHost _host = new();
    private readonly ReturnEntryManager _returns;
    private readonly ExpenseEntryManager _expenses;

    public RequestEntryTests()
    {
        _returns = new ReturnEntryManager(_host.Storage, _host.Outbox, _host.Clock);
        _expenses = new ExpenseEntryManager(_host.Storage, _host.Outbox, _host.Clock);
    }

    private static readonly RepStockCustodyBalanceDto[] Custody =
    [
        new(6, 3, "Concor 5mg", 11, "CON-01", Today.AddDays(40), 50, 12, 0, 38),
        new(6, 3, "Concor 5mg", 12, "CON-02", Today.AddDays(400), 10, 0, 0, 10),
        new(6, 7, "Ventolin", 13, "VEN-01", Today.AddDays(-20), 10, 0, 0, 10),
        new(6, 9, "Brufen", 14, "BRU-01", Today.AddDays(90), 5, 0, 5, 0)
    ];

    private static CollectionPhoto Receipt(string label) => new(label, "r.jpg", "image/jpeg", Convert.ToBase64String([0xFF, 0xD8, 1, 0xFF, 0xD9]));

    private static JsonElement Json(string? body) => JsonDocument.Parse(body!).RootElement;

    [Fact]
    public void Only_stock_in_hand_can_go_back_to_the_warehouse()
    {
        Assert.DoesNotContain(RequestRules.Returnable(Custody), c => c.ProductName == "Brufen");   // balance 0

        var draft = new ReturnDraft { Flow = ReturnFlowType.RepresentativeToWarehouse, WarehouseId = 1, ProductId = 3, ProductBatchId = 12, Quantity = 11 };
        Assert.Contains(RequestRules.Validate(draft, Custody), p => p.Contains("only hold 10"));

        draft.ProductBatchId = null;   // whole product: 38 + 10
        draft.Quantity = 48;
        Assert.Empty(RequestRules.Validate(draft, Custody));
    }

    [Fact]
    public void A_customer_return_needs_the_pharmacy_and_other_needs_notes()
    {
        var draft = new ReturnDraft { Flow = ReturnFlowType.CustomerToRepresentative, ProductId = 3, Quantity = 2, Reason = ReturnReason.Other };
        var problems = RequestRules.Validate(draft, Custody);
        Assert.Contains(problems, p => p.Contains("pharmacy"));
        Assert.Contains(problems, p => p.Contains("notes"));
    }

    [Fact]
    public async Task Return_is_queued_with_only_the_fields_of_its_flow()
    {
        await _returns.SendAsync(new ReturnDraft
        {
            Flow = ReturnFlowType.RepresentativeToWarehouse, PharmacyId = 5, WarehouseId = 2, ProductId = 7, ProductBatchId = 13,
            Quantity = 10, Reason = ReturnReason.Expired, ProductName = "Ventolin", CounterpartName = "Main warehouse"
        });

        var item = Assert.Single(_host.OutboxStore.Items);
        var body = Json(item.JsonBody);
        Assert.Equal("api/v1/Returns", item.Url);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("pharmacyId").ValueKind);
        Assert.Equal(2, body.GetProperty("warehouseId").GetInt32());
        Assert.Equal("Expired", body.GetProperty("reason").GetString());
        Assert.Single(await _returns.GetPendingAsync());
    }

    [Fact]
    public void Expense_rules()
    {
        var draft = new ExpenseDraft { Type = ExpenseType.ClientEntertainment, Amount = 450, ExpenseDate = Today.AddDays(1), TerritoryId = 1 };
        var problems = RequestRules.Validate(draft, Today);
        Assert.Contains(problems, p => p.Contains("future"));
        Assert.Contains(problems, p => p.Contains("entertained"));

        Assert.Empty(RequestRules.Validate(new ExpenseDraft { Type = ExpenseType.Fuel, Amount = 300, ExpenseDate = Today, TerritoryId = 1 }, Today));
    }

    [Fact]
    public async Task Expense_with_two_receipts_recorded_offline_reaches_the_server_with_their_file_ids()
    {
        _host.Api.Offline = true;
        var draft = new ExpenseDraft
        {
            Type = ExpenseType.Fuel, Amount = 320, ExpenseDate = Today, TerritoryId = 1, Description = "Cairo North route",
            Receipts = [Receipt("Receipt"), Receipt("Receipt (page 2)")]
        };
        await _expenses.SendAsync(draft, submit: true, Today);
        Assert.Equal(3, _host.OutboxStore.Items.Count);

        _host.Api.Respond("api/v1/Files", () => FakeApi.Status(System.Net.HttpStatusCode.Created, """{"id":501}"""),
            () => FakeApi.Status(System.Net.HttpStatusCode.Created, """{"id":502}"""));
        _host.Api.Offline = false;
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        await _host.Processor.RunOnceAsync();

        Assert.Equal(["api/v1/Files", "api/v1/Files", "api/v1/Expenses"], _host.Api.Requests.Select(r => r.Path));
        Assert.Contains("name=entityType", _host.Api.Requests[0].Body);
        Assert.Contains("Expense", _host.Api.Requests[0].Body);
        var expense = Json(_host.Api.Requests[2].Body);
        Assert.Equal([501, 502], expense.GetProperty("attachmentIds").EnumerateArray().Select(e => e.GetInt32()));
        Assert.True(expense.GetProperty("submit").GetBoolean());
        Assert.Equal("Fuel", expense.GetProperty("type").GetString());
        Assert.Empty(_host.OutboxStore.Files);
        Assert.Empty(await _expenses.GetPendingAsync());
    }

    [Fact]
    public async Task A_rejected_receipt_holds_back_the_expense_instead_of_sending_it_without_it()
    {
        await _expenses.SendAsync(new ExpenseDraft
        {
            Type = ExpenseType.Meals, Amount = 120, ExpenseDate = Today, TerritoryId = 1, Receipts = [Receipt("Receipt")]
        }, submit: true, Today);
        _host.Api.Respond("api/v1/Files", () => FakeApi.Status(System.Net.HttpStatusCode.BadRequest, """{"title":"Only JPEG, PNG, or PDF files can be attached."}"""));

        await _host.Processor.RunOnceAsync();

        Assert.Single(_host.Api.Requests);
        Assert.Single(await _expenses.GetPendingAsync());
    }

    [Fact]
    public async Task Submitting_a_server_draft_is_queued_and_shown_as_pending()
    {
        await _expenses.SubmitDraftAsync(88, "Fuel");

        Assert.Equal("api/v1/Expenses/88/submit", Assert.Single(_host.OutboxStore.Items).Url);
        Assert.Contains(88, await _expenses.GetPendingSubmitsAsync());
    }
}

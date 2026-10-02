using Microsoft.Extensions.Logging;
using PharmaERP.Application.Expenses;
using PharmaERP.Application.Returns;
using PharmaERP.Application.Warehouses;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Storage;

namespace PharmaERP.FieldApp.UI.Services.Requests;

/// <summary>Return requests from the field (wireframe 11), queued like every other write.</summary>
public sealed class ReturnEntryManager(KeyValueStore storage, Outbox outbox, TimeProvider time)
{
    public const string OutboxKind = "ReturnRequest";

    private List<PendingReturn>? _pending;

    public event Action? Changed;

    public async Task<PendingReturn> SendAsync(ReturnDraft draft)
    {
        if (RequestRules.Validate(draft, null) is { Count: > 0 } problems) throw new InvalidOperationException(problems[0]);

        var item = await outbox.EnqueueAsync(new OutboxRequest(
            Kind: OutboxKind,
            Title: $"Return · {draft.ProductName} × {draft.Quantity} · {draft.CounterpartName}",
            Method: "POST",
            Url: "api/v1/Returns",
            Body: new
            {
                flowType = draft.Flow,
                pharmacyId = draft.Flow == Domain.Enums.ReturnFlowType.CustomerToRepresentative ? draft.PharmacyId : null,
                warehouseId = draft.Flow == Domain.Enums.ReturnFlowType.RepresentativeToWarehouse ? draft.WarehouseId : null,
                productId = draft.ProductId,
                productBatchId = draft.ProductBatchId,
                quantity = draft.Quantity,
                reason = draft.Reason,
                notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim()
            }));

        var pending = new PendingReturn(item.Id, draft.Flow, draft.CounterpartName, draft.ProductName, draft.Quantity,
            draft.Reason, time.GetUtcNow().UtcDateTime);
        var list = await LoadAsync();
        list.Insert(0, pending);
        await storage.SetAsync(StorageKeys.PendingReturns, list);
        Changed?.Invoke();
        return pending;
    }

    public async Task<IReadOnlyList<PendingReturn>> GetPendingAsync()
    {
        var list = await LoadAsync();
        var queued = (await outbox.GetItemsAsync()).Select(i => i.Id).ToHashSet();
        if (list.RemoveAll(p => !queued.Contains(p.OutboxId)) > 0) await storage.SetAsync(StorageKeys.PendingReturns, list);
        return list;
    }

    public void Reset()
    {
        _pending = null;
        Changed?.Invoke();
    }

    private async Task<List<PendingReturn>> LoadAsync() =>
        _pending ??= await storage.GetAsync<List<PendingReturn>>(StorageKeys.PendingReturns) ?? [];
}

/// <summary>Expenses from the field (wireframe 12). Receipts are uploaded first, each waiting for the one before;
/// the expense then names them by their outbox refs, resolved to the server's file ids before it is sent:
///
///   POST api/v1/Files      (multipart, EntityType=Expense)         → ref file:{guid}   (one per receipt)
///   POST api/v1/Expenses   { …, attachmentIds: [{ref:file:…}], submit }</summary>
public sealed class ExpenseEntryManager(KeyValueStore storage, Outbox outbox, TimeProvider time)
{
    public const string OutboxKind = "Expense";
    public const string ReceiptKind = "ExpenseReceipt";
    public const string SubmitKind = "ExpenseSubmit";

    private List<PendingExpense>? _pending;

    public event Action? Changed;

    public async Task<PendingExpense> SendAsync(ExpenseDraft draft, bool submit, DateOnly today)
    {
        if (RequestRules.Validate(draft, today) is { Count: > 0 } problems) throw new InvalidOperationException(problems[0]);

        Guid? previous = null;
        var receiptRefs = new List<string>();
        foreach (var (receipt, index) in draft.Receipts.Select((r, i) => (r, i)))
        {
            var reference = $"file:{draft.LocalId}:{index}";
            var item = await outbox.EnqueueAsync(new OutboxRequest(
                Kind: ReceiptKind,
                Title: $"Receipt {index + 1} · {RequestRules.Label(draft.Type)} expense",
                Method: "POST",
                Url: "api/v1/Files",
                Body: new { entityType = "Expense" },
                DependsOn: previous,
                ProducesRef: reference,
                File: new Offline.OutboxFile($"receipt-{index + 1}.jpg", receipt.ContentType, receipt.Base64)));
            previous = item.Id;
            receiptRefs.Add(OutboxRefs.Placeholder(reference));
        }

        var expense = await outbox.EnqueueAsync(new OutboxRequest(
            Kind: OutboxKind,
            Title: $"{(submit ? "Expense" : "Draft expense")} · {RequestRules.Label(draft.Type)} · EGP {draft.Amount:N0}",
            Method: "POST",
            Url: "api/v1/Expenses",
            Body: new
            {
                type = draft.Type,
                amount = draft.Amount,
                expenseDate = draft.ExpenseDate,
                territoryId = draft.TerritoryId,
                description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim(),
                attachmentIds = receiptRefs,
                submit
            },
            DependsOn: previous));   // waits for every receipt (they're chained)

        var pending = new PendingExpense(draft.LocalId, expense.Id, draft.Type, draft.Amount, draft.ExpenseDate, submit,
            draft.Receipts.Count, time.GetUtcNow().UtcDateTime);
        var list = await LoadAsync();
        list.Insert(0, pending);
        await storage.SetAsync(StorageKeys.PendingExpenses, list);
        Changed?.Invoke();
        return pending;
    }

    /// <summary>Submits a draft that's already on the server.</summary>
    public async Task SubmitDraftAsync(int expenseId, string title)
    {
        await outbox.EnqueueAsync(new OutboxRequest(SubmitKind, $"Submit expense #{expenseId} · {title}", "POST",
            $"api/v1/Expenses/{expenseId}/submit"));
        Changed?.Invoke();
    }

    public async Task<IReadOnlySet<int>> GetPendingSubmitsAsync() =>
        (await outbox.GetItemsAsync()).Where(i => i.Kind == SubmitKind)
            .Select(i => int.TryParse(i.Url.Split('/')[^2], out var id) ? id : 0).Where(id => id > 0).ToHashSet();

    public async Task<IReadOnlyList<PendingExpense>> GetPendingAsync()
    {
        var list = await LoadAsync();
        var queued = (await outbox.GetItemsAsync()).Select(i => i.Id).ToHashSet();
        if (list.RemoveAll(p => !queued.Contains(p.ExpenseOutboxId)) > 0) await storage.SetAsync(StorageKeys.PendingExpenses, list);
        return list;
    }

    public void Reset()
    {
        _pending = null;
        Changed?.Invoke();
    }

    private async Task<List<PendingExpense>> LoadAsync() =>
        _pending ??= await storage.GetAsync<List<PendingExpense>>(StorageKeys.PendingExpenses) ?? [];
}

/// <summary>The rep's returns and expenses as the server knows them, and the warehouse list — cached for offline.</summary>
public sealed class RequestsStore(ReturnsApi returnsApi, ExpensesApi expensesApi, WarehousesApi warehousesApi,
    KeyValueStore storage, TimeProvider time, ILogger<RequestsStore> logger)
{
    public Task<Cached<IReadOnlyList<ReturnTransactionDto>>?> GetReturnsAsync(bool refresh, CancellationToken ct = default) =>
        GetAsync(StorageKeys.Returns, refresh, returnsApi.GetMineAsync, ct);

    public Task<Cached<IReadOnlyList<ExpenseDto>>?> GetExpensesAsync(bool refresh, CancellationToken ct = default) =>
        GetAsync(StorageKeys.Expenses, refresh, c => expensesApi.GetMineAsync(ct: c), ct);

    public Task<Cached<IReadOnlyList<WarehouseListItemDto>>?> GetWarehousesAsync(bool refresh, CancellationToken ct = default) =>
        GetAsync(StorageKeys.Warehouses, refresh, warehousesApi.GetAllAsync, ct);

    private async Task<Cached<IReadOnlyList<T>>?> GetAsync<T>(string key, bool refresh,
        Func<CancellationToken, Task<IReadOnlyList<T>>> fetch, CancellationToken ct)
    {
        if (refresh)
        {
            try
            {
                var fresh = new Cached<IReadOnlyList<T>>(await fetch(ct), time.GetUtcNow().UtcDateTime);
                await storage.SetAsync(key, fresh);
                return fresh;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogInformation("{Key} not refreshed: {Error}", key, ex.Message);
            }
        }
        return await storage.GetAsync<Cached<IReadOnlyList<T>>>(key);
    }
}

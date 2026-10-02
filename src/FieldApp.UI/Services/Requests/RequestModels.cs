using PharmaERP.Application.Custody;
using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Collections;

namespace PharmaERP.FieldApp.UI.Services.Requests;

/// <summary>Process Return (wireframe 11). Customer → rep: a pharmacy hands stock back. Rep → warehouse: the rep
/// hands custody stock back. Both wait for a manager's approval before any stock moves.</summary>
public sealed class ReturnDraft
{
    public ReturnFlowType Flow { get; set; } = ReturnFlowType.CustomerToRepresentative;
    public int? PharmacyId { get; set; }
    public int? WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; } = 1;
    public ReturnReason Reason { get; set; } = ReturnReason.Damaged;
    public string? Notes { get; set; }

    // For display / the pending list.
    public string ProductName { get; set; } = "";
    public string CounterpartName { get; set; } = "";
}

/// <summary>Submit Expense (wireframe 12).</summary>
public sealed class ExpenseDraft
{
    public Guid LocalId { get; init; } = Guid.NewGuid();
    public ExpenseType Type { get; set; } = ExpenseType.Travel;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public int TerritoryId { get; set; }
    public string? Description { get; set; }
    public List<CollectionPhoto> Receipts { get; set; } = [];
}

public sealed record PendingReturn(Guid OutboxId, ReturnFlowType Flow, string CounterpartName, string ProductName,
    int Quantity, ReturnReason Reason, DateTime QueuedAtUtc);

public sealed record PendingExpense(Guid LocalId, Guid ExpenseOutboxId, ExpenseType Type, decimal Amount,
    DateOnly ExpenseDate, bool Submitted, int ReceiptCount, DateTime QueuedAtUtc);

public static class RequestRules
{
    public const int MaxReceipts = 2;

    /// <summary>What the rep can hand back to the warehouse: custody lines with stock, per batch.</summary>
    public static IReadOnlyList<RepStockCustodyBalanceDto> Returnable(IEnumerable<RepStockCustodyBalanceDto> custody) =>
        custody.Where(c => c.Balance > 0).OrderBy(c => c.ProductName).ThenBy(c => c.ExpiryDate).ToList();

    public static IReadOnlyList<string> Validate(ReturnDraft d, IReadOnlyList<RepStockCustodyBalanceDto>? custody)
    {
        var problems = new List<string>();
        if (d.Flow == ReturnFlowType.CustomerToRepresentative && d.PharmacyId is not > 0) problems.Add("Choose the pharmacy returning the stock.");
        if (d.Flow == ReturnFlowType.RepresentativeToWarehouse && d.WarehouseId is not > 0) problems.Add("Choose the warehouse.");
        if (d.ProductId <= 0) problems.Add("Choose the product.");
        if (d.Quantity <= 0) problems.Add("Enter a quantity of at least 1.");
        if (d.Flow == ReturnFlowType.RepresentativeToWarehouse && custody is not null && d.ProductId > 0)
        {
            var held = custody.Where(c => c.ProductId == d.ProductId && (d.ProductBatchId is null || c.ProductBatchId == d.ProductBatchId))
                .Sum(c => c.Balance);
            if (d.Quantity > held) problems.Add($"You only hold {held} of this {(d.ProductBatchId is null ? "product" : "batch")}.");
        }
        if (d.Reason == ReturnReason.Other && string.IsNullOrWhiteSpace(d.Notes)) problems.Add("Describe the reason in the notes.");
        return problems;
    }

    public static IReadOnlyList<string> Validate(ExpenseDraft d, DateOnly today)
    {
        var problems = new List<string>();
        if (d.Amount <= 0) problems.Add("Enter the amount.");
        if (d.ExpenseDate > today) problems.Add("The expense date can't be in the future.");
        if (d.TerritoryId <= 0) problems.Add("Your territory isn't known yet — connect once to load your profile.");
        if (d.Type is ExpenseType.Other or ExpenseType.ClientEntertainment && string.IsNullOrWhiteSpace(d.Description))
            problems.Add(d.Type == ExpenseType.ClientEntertainment ? "Say who you entertained." : "Describe the expense.");
        if (d.Receipts.Count > MaxReceipts) problems.Add($"At most {MaxReceipts} receipt photos.");
        return problems;
    }

    public static string Label(ExpenseType t) => t switch
    {
        ExpenseType.ClientEntertainment => "Client entertainment",
        _ => t.ToString()
    };

    public static string Label(ReturnFlowType f) => f == ReturnFlowType.CustomerToRepresentative ? "From customer" : "To warehouse";
}

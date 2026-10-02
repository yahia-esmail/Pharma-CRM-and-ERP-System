using PharmaERP.Domain.Enums;

namespace PharmaERP.FieldApp.UI.Services.Orders;

/// <summary>One line being entered (wireframe 9). <see cref="UnitPrice"/> is the catalogue price cached on the
/// phone, for display only — the server prices every line itself.</summary>
public sealed record DraftLine(int ProductId, string ProductName, decimal UnitPrice, int Quantity = 1,
    int BonusQuantity = 0, decimal DiscountPercent = 0)
{
    public decimal Gross => OrderMath.Gross(Quantity, UnitPrice);
    public decimal Net => OrderMath.Net(Quantity, UnitPrice, DiscountPercent);
}

/// <summary>An order being edited on the phone. Saved locally as it changes, so closing the app mid-entry
/// loses nothing; it only leaves the phone when the rep taps Save draft or Submit.</summary>
public sealed class OrderDraft
{
    public Guid LocalId { get; init; } = Guid.NewGuid();

    /// <summary>Set when editing an order that already exists on the server (a Draft).</summary>
    public int? ServerId { get; init; }

    public int PharmacyId { get; set; }
    public string PharmacyName { get; set; } = "";

    /// <summary>The pharmacy visit this order is taken during (its outbox ref), if any.</summary>
    public Guid? VisitLocalId { get; set; }

    public List<DraftLine> Lines { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }

    public OrderTotals Totals => OrderMath.Totals(Lines);
    public bool IsEmpty => Lines.Count == 0;
}

public sealed record OrderTotals(decimal Subtotal, decimal Discount, decimal Net, int Units, int BonusUnits);

/// <summary>Mirrors the server's formula (OrderService): line total = qty × price × (1 − discount%). Bonus units
/// are free. Used to show totals while typing; the server's figures are the official ones.</summary>
public static class OrderMath
{
    public static decimal Gross(int quantity, decimal unitPrice) => quantity * unitPrice;

    public static decimal Net(int quantity, decimal unitPrice, decimal discountPercent) =>
        quantity * unitPrice * (1 - discountPercent / 100m);

    public static OrderTotals Totals(IEnumerable<DraftLine> lines)
    {
        decimal subtotal = 0, net = 0;
        int units = 0, bonus = 0;
        foreach (var l in lines)
        {
            subtotal += l.Gross;
            net += l.Net;
            units += l.Quantity;
            bonus += l.BonusQuantity;
        }
        return new OrderTotals(subtotal, subtotal - net, net, units, bonus);
    }

    /// <summary>True when the order would push the pharmacy past its credit limit (0 = no limit). The server
    /// decides at approval; this is only the early warning (plan phase 7).</summary>
    public static bool ExceedsCredit(decimal outstanding, decimal? creditLimit, decimal orderNet) =>
        creditLimit is > 0 and var limit && outstanding + orderNet > limit;
}

/// <summary>An order that has left the editor but not yet reached the server (it's in the outbox).</summary>
public sealed record PendingOrder(
    Guid LocalId,
    Guid OutboxId,
    int? ServerId,
    string PharmacyName,
    int LineCount,
    decimal Net,
    bool Submitted,
    DateTime QueuedAtUtc,
    OrderDraft Draft);

public static class OrderStatusText
{
    /// <summary>The stepper in wireframe 9: Draft → Submitted → Approved → Delivered.</summary>
    public static readonly OrderStatus[] Steps = [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Approved, OrderStatus.Delivered];

    public static bool CanEdit(OrderStatus s) => s == OrderStatus.Draft;
    public static bool CanCancel(OrderStatus s) => s is OrderStatus.Draft or OrderStatus.Submitted;

    public static string CssClass(OrderStatus s) => s switch
    {
        OrderStatus.Draft => "draft",
        OrderStatus.Submitted => "submitted",
        OrderStatus.Approved => "approved",
        OrderStatus.Delivered => "delivered",
        OrderStatus.Rejected => "rejected",
        _ => "cancelled"
    };
}

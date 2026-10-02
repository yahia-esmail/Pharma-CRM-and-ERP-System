using PharmaERP.Application.Visits;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Orders;

public record OrderListItemDto(
    int Id,
    int PharmacyId,
    string PharmacyName,
    int RepresentativeId,
    string RepresentativeName,
    DateTime OrderDateUtc,
    OrderStatus Status,
    decimal TotalAmount,
    // Shown on the field app's order cards (wireframe 4).
    string? RejectionReason = null,
    int LineCount = 0);

public record OrderLineDto(
    int Id,
    int ProductId,
    string ProductName,
    int Quantity,
    int BonusQuantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal);

public record OrderDetailDto(
    int Id,
    int PharmacyId,
    string PharmacyName,
    int RepresentativeId,
    string RepresentativeName,
    DateTime OrderDateUtc,
    OrderStatus Status,
    string? RejectionReason,
    IReadOnlyList<OrderLineDto> Lines,
    decimal TotalAmount,
    int? SaleId,
    DateTime? SubmittedAtUtc = null,
    int? PharmacyVisitId = null);

public class OrderCreateRequest
{
    public int PharmacyId { get; set; }
}

public class OrderUpdateRequest
{
    public int PharmacyId { get; set; }
}

public class OrderLineSaveRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int BonusQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
}

/// <summary>A whole draft in one request (field app, plan phase 7): header, every line and optionally the
/// submit, applied in one transaction. Sent through the offline outbox, so a half-applied order — created
/// but missing lines, or lines saved but never submitted — can't be left behind by a dropped connection.
/// Prices are never taken from the client; each line is priced from the catalogue on the server.</summary>
public class OrderSaveRequest
{
    public int PharmacyId { get; set; }
    public List<OrderLineSaveRequest> Lines { get; set; } = [];

    /// <summary>Send for approval right after saving.</summary>
    public bool Submit { get; set; }

    /// <summary>The visit the order was taken during (must be the caller's, at the same pharmacy).</summary>
    public int? PharmacyVisitId { get; set; }

    /// <summary>The rep's position when submitting, if the phone had one.</summary>
    public VisitFix? Location { get; set; }
}

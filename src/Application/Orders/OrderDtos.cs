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
    decimal TotalAmount);

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
    int? SaleId);

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

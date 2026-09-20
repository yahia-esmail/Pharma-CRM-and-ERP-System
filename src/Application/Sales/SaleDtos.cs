namespace PharmaERP.Application.Sales;

public record SaleListItemDto(
    int Id,
    int OrderId,
    int PharmacyId,
    string PharmacyName,
    int RepresentativeId,
    string RepresentativeName,
    DateTime SaleDateUtc,
    decimal TotalAmount);

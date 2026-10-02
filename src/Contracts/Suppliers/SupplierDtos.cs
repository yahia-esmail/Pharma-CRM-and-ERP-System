using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Suppliers;

public record SupplierListItemDto(int Id, string Name, string? ContactName, string? Phone, decimal OutstandingBalance, SupplierStatus Status);

public record SupplierDetailDto(
    int Id, string Name, string? ContactName, string? Phone, string? Email,
    string? TaxRegistrationNumber, int PaymentTermDays, SupplierStatus Status);

public class SupplierSaveRequest
{
    public string Name { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public SupplierStatus Status { get; set; } = SupplierStatus.Active;
}

public record SupplierLedgerLineDto(string Type, DateTime DateUtc, decimal Amount, string? Reference);

/// <summary>Computed, never stored (spec 5.3).</summary>
public record SupplierLedgerDto(
    int SupplierId, decimal TotalPurchased, decimal TotalPaid, decimal OutstandingBalance,
    IReadOnlyList<SupplierLedgerLineDto> Lines);

public record SupplierPaymentDto(int Id, int SupplierId, string SupplierName, decimal Amount,
    DateTime PaymentDateUtc, PaymentMethod PaymentMethod, string? ReferenceNumber, string? Notes);

public class SupplierPaymentSaveRequest
{
    public int SupplierId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

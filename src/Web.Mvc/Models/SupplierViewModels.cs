using System.ComponentModel.DataAnnotations;
using PharmaERP.Application.Purchasing;
using PharmaERP.Application.Suppliers;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(150)]
    public string? ContactName { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? TaxRegistrationNumber { get; set; }

    [Range(0, 365)]
    public int PaymentTermDays { get; set; } = 30;

    public SupplierStatus Status { get; set; } = SupplierStatus.Active;
}

public class SupplierPaymentEntryViewModel
{
    [Required]
    public int SupplierId { get; set; }

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class SupplierDetailsViewModel
{
    public SupplierDetailDto Supplier { get; set; } = null!;
    public SupplierLedgerDto Ledger { get; set; } = null!;
    public IReadOnlyList<PurchaseOrderListItemDto> PurchaseOrders { get; set; } = [];
    public SupplierPaymentEntryViewModel NewPayment { get; set; } = new();
}

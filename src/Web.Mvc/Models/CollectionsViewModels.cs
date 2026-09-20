using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.Collections;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class CollectionEntryViewModel
{
    [Required]
    public int PharmacyId { get; set; }

    public int? SaleId { get; set; }

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class RemittanceEntryViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentMethod RemittanceMethod { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class FinancialReconciliationRequestViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    [Required]
    public decimal CountedBalance { get; set; }

    public string? Reason { get; set; }
}

public class FinancialCustodyIndexViewModel
{
    public int RepresentativeId { get; set; }
    public IReadOnlyList<RepFinancialCustodyDto> Summary { get; set; } = [];
    public RepFinancialCustodyDto? Selected { get; set; }
    public IReadOnlyList<CollectionDto> Collections { get; set; } = [];
    public IReadOnlyList<RemittanceTransactionDto> Remittances { get; set; } = [];
    public IReadOnlyList<FinancialReconciliationDto> Reconciliations { get; set; } = [];

    public CollectionEntryViewModel NewCollection { get; set; } = new();
    public RemittanceEntryViewModel NewRemittance { get; set; } = new();
    public FinancialReconciliationRequestViewModel NewReconciliation { get; set; } = new();

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Pharmacies { get; set; } = [];
}

public class CollectionAttachmentsViewModel
{
    public int CollectionId { get; set; }
    public int RepresentativeId { get; set; }
    public string PharmacyName { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime CollectionDateUtc { get; set; }
    public IReadOnlyList<CollectionAttachmentDto> Attachments { get; set; } = [];
}

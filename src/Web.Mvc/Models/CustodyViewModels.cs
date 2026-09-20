using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.Custody;

namespace PharmaERP.Web.Mvc.Models;

public class IssueToCustodyViewModel
{
    [Required]
    public int WarehouseId { get; set; }

    [Required]
    public int RepresentativeId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public string? ReferenceNote { get; set; }
}

public class ReturnFromCustodyViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Required]
    public string ReasonCode { get; set; } = null!;
}

public class CustodyTransferViewModel
{
    [Required]
    public int FromRepresentativeId { get; set; }

    [Required]
    public int ToRepresentativeId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

public class ReconciliationViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required]
    public int CountedBalance { get; set; }

    public string? Notes { get; set; }
}

public class CustodyIndexViewModel
{
    public int RepresentativeId { get; set; }
    public string RepresentativeName { get; set; } = null!;
    public IReadOnlyList<RepStockCustodyBalanceDto> Balances { get; set; } = [];
    public IReadOnlyList<CustodyTransactionDto> Ledger { get; set; } = [];
    public IReadOnlyList<RepStockReservationDto> Reservations { get; set; } = [];

    public IssueToCustodyViewModel Issue { get; set; } = new();
    public ReturnFromCustodyViewModel Return { get; set; } = new();
    public CustodyTransferViewModel Transfer { get; set; } = new();
    public ReconciliationViewModel Reconcile { get; set; } = new();

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Warehouses { get; set; } = [];
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

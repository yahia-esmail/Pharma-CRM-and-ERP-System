using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.Returns;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class ReturnRequestViewModel
{
    public int RepresentativeId { get; set; }

    [Required]
    public ReturnFlowType FlowType { get; set; }

    public int? PharmacyId { get; set; }
    public int? WarehouseId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public ReturnReason Reason { get; set; }
    public string? Notes { get; set; }
}

public class ReturnsIndexViewModel
{
    public int RepresentativeId { get; set; }
    public string? RepresentativeName { get; set; }
    public IReadOnlyList<ReturnTransactionDto> Returns { get; set; } = [];
    public ReturnRequestViewModel NewRequest { get; set; } = new();

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Pharmacies { get; set; } = [];
    public IEnumerable<SelectListItem> Warehouses { get; set; } = [];
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

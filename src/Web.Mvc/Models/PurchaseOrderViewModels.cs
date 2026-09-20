using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.Purchasing;

namespace PharmaERP.Web.Mvc.Models;

public class PurchaseOrderCreateViewModel
{
    [Required]
    public int SupplierId { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    public IEnumerable<SelectListItem> Suppliers { get; set; } = [];
}

public class PurchaseOrderAddLineViewModel
{
    public int PurchaseOrderId { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Required, Range(0.01, double.MaxValue)]
    public decimal UnitCost { get; set; }
}

/// <summary>Receives one PO line at a time — a simple, repeatable form rather than a multi-row array post.</summary>
public class PurchaseReceiptFormViewModel
{
    public int PurchaseOrderId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    [Required]
    public int PurchaseOrderLineId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public string? BatchNumber { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public IEnumerable<SelectListItem> Warehouses { get; set; } = [];
    public IEnumerable<SelectListItem> OrderLines { get; set; } = [];
}

public class PurchaseOrderDetailsViewModel
{
    public PurchaseOrderDetailDto Order { get; set; } = null!;
    public PurchaseOrderAddLineViewModel NewLine { get; set; } = new();
    public PurchaseReceiptFormViewModel Receipt { get; set; } = new();
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

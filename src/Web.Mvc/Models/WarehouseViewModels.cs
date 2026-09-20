using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class WarehouseFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string? Location { get; set; }

    public WarehouseType Type { get; set; } = WarehouseType.Main;
}

public class GoodsReceiptViewModel
{
    [Required]
    public int WarehouseId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public string? BatchNumber { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public string? ReferenceNote { get; set; }
}

public class StockTransferViewModel
{
    [Required]
    public int SourceWarehouseId { get; set; }

    [Required]
    public int DestinationWarehouseId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public string? ReferenceNote { get; set; }
}

public class StockAdjustmentViewModel
{
    [Required]
    public int WarehouseId { get; set; }

    [Required]
    public int ProductId { get; set; }

    public int? ProductBatchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public bool IsIncrease { get; set; } = true;

    [Required]
    public string ReasonCode { get; set; } = null!;
}

public class WarehouseOperationsViewModel
{
    public GoodsReceiptViewModel GoodsReceipt { get; set; } = new();
    public StockTransferViewModel Transfer { get; set; } = new();
    public StockAdjustmentViewModel Adjustment { get; set; } = new();

    public IEnumerable<SelectListItem> Warehouses { get; set; } = [];
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

public class ProductBatchFormViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required, StringLength(50)]
    public string BatchNumber { get; set; } = null!;

    public DateOnly? ManufactureDate { get; set; }

    [Required]
    public DateOnly ExpiryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));

    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

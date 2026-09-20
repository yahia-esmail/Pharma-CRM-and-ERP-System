using System.ComponentModel.DataAnnotations;

namespace PharmaERP.Web.Mvc.Models;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Sku { get; set; } = null!;

    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(100)]
    public string? Category { get; set; }

    [Required, StringLength(20)]
    public string UnitOfMeasure { get; set; } = null!;

    public bool IsBatchTracked { get; set; }
    public bool IsExpiryTracked { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}

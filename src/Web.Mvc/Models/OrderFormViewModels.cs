using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.Orders;

namespace PharmaERP.Web.Mvc.Models;

public class OrderCreateViewModel
{
    [Required]
    public int PharmacyId { get; set; }

    public IEnumerable<SelectListItem> Pharmacies { get; set; } = [];
}

public class OrderAddLineViewModel
{
    public int OrderId { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int BonusQuantity { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }
}

public class OrderDetailsViewModel
{
    public OrderDetailDto Order { get; set; } = null!;
    public OrderAddLineViewModel NewLine { get; set; } = new();
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class TerritoryFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(150)]
    public string? Region { get; set; }

    public string? Description { get; set; }
    public TerritoryType Type { get; set; } = TerritoryType.District;
    public int? ParentTerritoryId { get; set; }
    public int? DistrictManagerId { get; set; }

    public IEnumerable<SelectListItem> Managers { get; set; } = [];
    public IEnumerable<SelectListItem> ParentTerritories { get; set; } = [];
}

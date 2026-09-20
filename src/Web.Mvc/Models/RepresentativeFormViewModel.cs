using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class RepresentativeFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string EmployeeCode { get; set; } = null!;

    [Required, StringLength(200)]
    public string FullName { get; set; } = null!;

    public RepresentativeLevel Level { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public int? TerritoryId { get; set; }
    public int? ReportingManagerId { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public IEnumerable<SelectListItem> Territories { get; set; } = [];
    public IEnumerable<SelectListItem> Managers { get; set; } = [];
}

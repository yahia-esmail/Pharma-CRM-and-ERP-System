using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class ListFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    public CustomerListType Type { get; set; }
    public CustomerListMode Mode { get; set; }
    public int? OwnerRepresentativeId { get; set; }

    public int? FilterTerritoryId { get; set; }
    public int? FilterClassificationId { get; set; }
    public string? FilterSegment { get; set; }
    public bool FilterActiveOnly { get; set; } = true;

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Territories { get; set; } = [];
    public IEnumerable<SelectListItem> Classifications { get; set; } = [];
}

public class ListAddMemberViewModel
{
    public int ListId { get; set; }
    public int? DoctorId { get; set; }
    public int? PharmacyId { get; set; }
    public IEnumerable<SelectListItem> Doctors { get; set; } = [];
    public IEnumerable<SelectListItem> Pharmacies { get; set; } = [];
}

public class BulkAssignViewModel
{
    public int ListId { get; set; }
    [Required]
    public int ToRepresentativeId { get; set; }
    [StringLength(500)]
    public string? Reason { get; set; }
    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
}

public class CustomerTransferFormViewModel
{
    public int? DoctorId { get; set; }
    public int? PharmacyId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int? CurrentRepresentativeId { get; set; }
    public string? CurrentRepresentativeName { get; set; }

    [Required]
    public int ToRepresentativeId { get; set; }
    [StringLength(500)]
    public string? Reason { get; set; }

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
}

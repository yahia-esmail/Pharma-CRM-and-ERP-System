using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class DoctorFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string FullName { get; set; } = null!;

    [Required, StringLength(150)]
    public string Specialty { get; set; } = null!;

    [StringLength(150)]
    public string? SubSpecialty { get; set; }

    [StringLength(200)]
    public string? ClinicOrHospital { get; set; }

    public string? Address { get; set; }

    [StringLength(100)]
    public string? Governorate { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [Phone]
    public string? WhatsAppNumber { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public int? ClassificationId { get; set; }
    public string? PreferredVisitingDays { get; set; }
    public string? PreferredVisitingTimes { get; set; }
    public int? PrimaryRepresentativeId { get; set; }
    public int? TerritoryId { get; set; }
    public DoctorStatus Status { get; set; } = DoctorStatus.Prospect;

    /// <summary>When true, skip the duplicate check — the user already reviewed the warning and chose to proceed.</summary>
    public bool ConfirmDespiteDuplicates { get; set; }

    public IEnumerable<SelectListItem> Classifications { get; set; } = [];
    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Territories { get; set; } = [];
}

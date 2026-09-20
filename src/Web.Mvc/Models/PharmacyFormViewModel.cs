using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class PharmacyFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(100)]
    public string? LicenseNumber { get; set; }

    [StringLength(200)]
    public string? OwnerName { get; set; }

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

    [StringLength(20)]
    public string? Segment { get; set; }

    [Range(0, 365)]
    public int PaymentTermDays { get; set; } = 30;

    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }

    public int? PrimaryRepresentativeId { get; set; }
    public int? TerritoryId { get; set; }
    public PharmacyStatus Status { get; set; } = PharmacyStatus.Prospect;

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
    public IEnumerable<SelectListItem> Territories { get; set; } = [];
}

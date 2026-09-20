using System.ComponentModel.DataAnnotations;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class PharmacyVisitFormViewModel
{
    public int PharmacyId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime VisitDateUtc { get; set; } = DateTime.UtcNow;

    [Range(1, 480)]
    public int? DurationMinutes { get; set; }

    public PharmacyVisitPurpose Purpose { get; set; }
    public string? Notes { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace PharmaERP.Web.Mvc.Models;

public class DoctorVisitFormViewModel
{
    public int DoctorId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime VisitDateUtc { get; set; } = DateTime.UtcNow;

    [Range(1, 480)]
    public int? DurationMinutes { get; set; }

    public string? ProductsDiscussed { get; set; }
    public string? SamplesGiven { get; set; }
    public string? MaterialsLeft { get; set; }
    public string? FeedbackNotes { get; set; }
    public string? NextVisitRecommendation { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
}

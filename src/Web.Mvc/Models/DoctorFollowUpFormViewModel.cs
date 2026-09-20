using System.ComponentModel.DataAnnotations;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class DoctorFollowUpFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "A doctor must be selected.")]
    public int DoctorId { get; set; }
    public int? DoctorVisitId { get; set; }
    public FollowUpType Type { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.AddDays(3);

    public int OwnerRepresentativeId { get; set; }
}

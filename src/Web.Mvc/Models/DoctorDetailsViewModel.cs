using PharmaERP.Application.Doctors;

namespace PharmaERP.Web.Mvc.Models;

public class DoctorDetailsViewModel
{
    public DoctorDetailDto Doctor { get; set; } = null!;
    public IReadOnlyList<DoctorVisitDto> Visits { get; set; } = [];
    public IReadOnlyList<DoctorFollowUpDto> FollowUps { get; set; } = [];
    public DoctorVisitFormViewModel NewVisit { get; set; } = new();
    public DoctorFollowUpFormViewModel NewFollowUp { get; set; } = new();
}

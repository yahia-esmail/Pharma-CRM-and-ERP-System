using PharmaERP.Application.Doctors;
using PharmaERP.Shared.Common;

namespace PharmaERP.Web.Mvc.Models;

public class DoctorIndexViewModel
{
    public PagedResult<DoctorListItemDto> Result { get; set; } = null!;
    public string? Search { get; set; }
    public string? Specialty { get; set; }
    public string? City { get; set; }
}

using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.VisitPlans;

namespace PharmaERP.Web.Mvc.Models;

public class VisitPlanDetailsViewModel
{
    public VisitPlanDetailDto Plan { get; set; } = null!;
    public PlanVsActualDto? PlanVsActual { get; set; }
    public VisitPlanAddItemViewModel NewItem { get; set; } = new();
    public IEnumerable<SelectListItem> Doctors { get; set; } = [];
}

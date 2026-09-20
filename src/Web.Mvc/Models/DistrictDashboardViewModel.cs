using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Application.DistrictDashboard;

namespace PharmaERP.Web.Mvc.Models;

public class DistrictDashboardViewModel
{
    public DistrictDashboardDto? Summary { get; set; }
    public int? TerritoryId { get; set; }
    public IEnumerable<SelectListItem> Territories { get; set; } = [];
}

public class RepresentativeDrilldownViewModel
{
    public RepresentativeDrilldownDto Drilldown { get; set; } = null!;
}

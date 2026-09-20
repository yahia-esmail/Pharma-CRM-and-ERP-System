using PharmaERP.Application.Dashboard;

namespace PharmaERP.Web.Mvc.Models;

public class DashboardViewModel
{
    public DashboardSummaryDto Summary { get; set; } = null!;
    public int? TerritoryId { get; set; }
    public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Territories { get; set; } = [];
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Sales;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Infrastructure;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.SalesView)]
public class SalesController(ISalesService salesService) : Controller
{
    public async Task<IActionResult> Index(DateOnly? fromDate, DateOnly? toDate, int page = 1)
    {
        var result = await salesService.GetListAsync(new PagedRequest { PageNumber = page }, null, null, fromDate, toDate);
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        return View(result);
    }

    public async Task<IActionResult> Export(DateOnly? fromDate, DateOnly? toDate)
    {
        var result = await salesService.GetListAsync(
            new PagedRequest { PageNumber = 1, PageSize = 5000 }, null, null, fromDate, toDate);

        var csv = CsvExport.Build(
            ["Date", "Pharmacy", "Representative", "Order #", "Amount"],
            result.Items.Select(s => new object?[]
            {
                s.SaleDateUtc.ToString("yyyy-MM-dd"), s.PharmacyName, s.RepresentativeName, s.OrderId, s.TotalAmount
            }));

        return File(csv, "text/csv", $"sales-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }
}

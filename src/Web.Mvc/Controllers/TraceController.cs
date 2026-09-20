using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Traceability;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>"Trace This Transaction" (spec 4.11) — full chain from supplier PO through to collection/remittance.</summary>
[Authorize(Policy = Policies.TraceView)]
public class TraceController(ITraceService traceService) : Controller
{
    public async Task<IActionResult> Batch(int id)
    {
        try
        {
            return View(await traceService.TraceBatchAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> Sale(int id)
    {
        try
        {
            return View(await traceService.TraceSaleAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Warehouses;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Warehouses a representative can return stock to (plan 6.2 item 12, wireframe 11). Names and ids
/// only — stock levels stay with the Warehouse/manager roles in the dashboard.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.CustodyReturn)]
public class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseListItemDto>>> GetList(CancellationToken ct) =>
        Ok(await warehouses.GetListAsync(ct));
}

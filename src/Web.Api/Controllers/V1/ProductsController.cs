using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Products;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Product catalog for the mobile app's order-line and "products discussed" pickers (spec 4.10).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.ProductsView)]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        return Ok(await productService.GetListAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDetailDto>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await productService.GetByIdAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

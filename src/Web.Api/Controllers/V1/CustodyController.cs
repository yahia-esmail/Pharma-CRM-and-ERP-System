using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Custody;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>"My Stock Custody" screen for the mobile app (spec 4.10) — view balance/ledger, submit a return.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.CustodyView)]
public class CustodyController(ICustodyService custodyService) : ControllerBase
{
    [HttpGet("mine/balances")]
    public async Task<ActionResult<IReadOnlyList<RepStockCustodyBalanceDto>>> GetMyBalances(CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();
        return Ok(await custodyService.GetBalancesAsync(repId, ct));
    }

    [HttpGet("mine/ledger")]
    public async Task<ActionResult<IReadOnlyList<CustodyTransactionDto>>> GetMyLedger(
        [FromQuery] int? productId, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();
        return Ok(await custodyService.GetLedgerAsync(repId, productId, ct));
    }

    [HttpPost("mine/return")]
    [Authorize(Policy = Policies.CustodyReturn)]
    public async Task<ActionResult> SubmitReturn(ReturnFromCustodyRequest request, CancellationToken ct)
    {
        if (CurrentRepresentativeId() is not { } repId) return Forbid();

        try
        {
            request.RepresentativeId = repId;
            var movementId = await custodyService.ReturnAsync(request, ct);
            return Ok(new { movementId });
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    /// <summary>Records a physical stock count against a representative's custody (spec 4.5) — a
    /// Warehouse/manager action, not a self-service one, since it applies the variance immediately as an
    /// adjustment (see CustodyService.ReconcileAsync); a representative cannot self-adjust their own balance.</summary>
    [HttpPost("reconcile")]
    [Authorize(Policy = Policies.CustodyManage)]
    public async Task<ActionResult<StockReconciliationResultDto>> Reconcile(StockReconciliationRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await custodyService.ReconcileAsync(request, ct));
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    private int? CurrentRepresentativeId()
    {
        var claim = User.FindFirst("RepresentativeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

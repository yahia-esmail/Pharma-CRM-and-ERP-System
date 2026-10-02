using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>"My Financial Custody" + collection entry for the mobile app (spec 4.10). Everything here is the
/// signed-in representative's own: the representative id always comes from the token.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.CollectionsView)]
public class CollectionsController(ICollectionService collectionService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<CollectionDto>>> GetMine(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25,
        [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null, CancellationToken ct = default)
    {
        if (this.RepresentativeId() is not { } repId) return Forbid();
        return Ok(await collectionService.GetCollectionsAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize }, repId, null, dateFrom, dateTo, ct));
    }

    [HttpGet("mine/custody")]
    public Task<ActionResult> GetMyCustody(CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId => Ok(await collectionService.GetFinancialCustodyAsync(repId, ct)));

    /// <summary>Records a collection. Returns <c>{ id }</c> — the field app's outbox needs it to attach the
    /// proof-of-payment photos that were queued with it.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.CollectionsCreate)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public Task<ActionResult> Create(CollectionSaveRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            var id = await collectionService.RecordCollectionAsync(repId, request, ct);
            return CreatedAtAction(nameof(GetMine), null, new { id });
        });

    /// <summary>Proof of payment (receipt, cheque front/back) for one of the caller's collections.</summary>
    [HttpPost("{id:int}/attachments")]
    [Authorize(Policy = Policies.CollectionsCreate)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public Task<ActionResult> AddAttachment(int id, IFormFile file, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            if (currentUser.UserId is not { } userId) return Forbid();
            if (file is null || file.Length == 0) return BadRequest(new ProblemDetails { Title = "The uploaded file is empty." });
            await using var stream = file.OpenReadStream();
            var attachmentId = await collectionService.AddAttachmentAsync(id, userId, file.FileName, file.ContentType,
                file.Length, stream, ct, ownerRepresentativeId: repId);
            return Created($"api/v1/Collections/{id}/attachments/{attachmentId}", new { id = attachmentId });
        });

    [HttpGet("mine/reconciliations")]
    public Task<ActionResult> GetMyReconciliations(CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId => Ok(await collectionService.GetReconciliationsAsync(repId, null, ct)));

    /// <summary>The rep's own cash count against their financial custody (addendum 3.6). No difference: settled at
    /// once. A difference needs a reason and waits for a manager — the balance doesn't change until approved.</summary>
    [HttpPost("mine/reconciliations")]
    [Authorize(Policy = Policies.CollectionsCreate)]
    public Task<ActionResult> RequestMyReconciliation(MyFinancialReconciliationRequest request, CancellationToken ct) =>
        this.ForRepresentativeAsync(async repId =>
        {
            if (currentUser.UserId is not { } userId) return Forbid();
            var result = await collectionService.RequestReconciliationAsync(userId,
                new FinancialReconciliationRequest { RepresentativeId = repId, CountedBalance = request.CountedBalance, Reason = request.Reason }, ct);
            return Ok(result);
        });
}

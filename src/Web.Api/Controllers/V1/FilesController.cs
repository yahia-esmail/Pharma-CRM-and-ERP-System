using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Files;

namespace PharmaERP.Web.Api.Controllers.V1;

public class FileUploadForm
{
    public IFormFile File { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public int? EntityId { get; set; }
}

/// <summary>Generic file/photo upload for the mobile app (Expense receipts today; extensible to other
/// entity types as they adopt it). Any signed-in user may upload their own files — the module the file
/// gets linked to (e.g. Expenses) enforces who may attach it to a specific record.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class FilesController(IFileAttachmentService fileAttachments, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Upload([FromForm] FileUploadForm form, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        if (form.File.Length == 0) return BadRequest(new ProblemDetails { Title = "The uploaded file is empty." });

        try
        {
            await using var stream = form.File.OpenReadStream();
            var dto = await fileAttachments.UploadAsync(form.EntityType, form.EntityId, userId, form.File.FileName,
                form.File.ContentType, form.File.Length, stream, ct);
            return CreatedAtAction(nameof(Download), new { id = dto.Id }, dto);
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        try
        {
            var (content, contentType, fileName) = await fileAttachments.OpenAsync(id, ct);
            return File(content, contentType, fileName);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

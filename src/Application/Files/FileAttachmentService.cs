using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Files;

/// <summary>Generic entity-attachment upload/link/download, generalizing the pattern already used by
/// CollectionAttachment to any entity type (currently just "Expense"). Allowlisted rather than fully
/// open-ended so this doesn't silently become an attachment target for modules that haven't adopted it.</summary>
public class FileAttachmentService(IAppDbContext db, IFileStorageService fileStorage) : IFileAttachmentService
{
    private static readonly HashSet<string> AllowedEntityTypes = new(StringComparer.OrdinalIgnoreCase) { "Expense" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "application/pdf" };
    private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024;

    public async Task<FileAttachmentDto> UploadAsync(string entityType, int? entityId, string uploadedByUserId,
        string fileName, string contentType, long sizeBytes, Stream content, CancellationToken ct = default)
    {
        if (!AllowedEntityTypes.Contains(entityType))
            throw new ValidationFailedException($"Attachments are not supported for entity type '{entityType}'.");
        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationFailedException("Only JPEG, PNG, or PDF files can be attached.");
        if (sizeBytes > MaxAttachmentSizeBytes)
            throw new ValidationFailedException("Attachment must be 5 MB or smaller.");

        var relativePath = await fileStorage.SaveAsync($"{entityType.ToLowerInvariant()}/{entityId?.ToString() ?? "unlinked"}",
            fileName, content, ct);

        var attachment = new FileAttachment
        {
            EntityType = entityType,
            EntityId = entityId,
            FileName = fileName,
            ContentType = contentType,
            RelativePath = relativePath,
            SizeBytes = sizeBytes,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByUserId = uploadedByUserId
        };
        db.FileAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        return ToDto(attachment);
    }

    public async Task EnsureLinkableAsync(string entityType, IReadOnlyList<int> attachmentIds, string requestingUserId,
        CancellationToken ct = default) =>
        await LoadLinkableAsync(entityType, attachmentIds, requestingUserId, ct);

    public async Task LinkAsync(string entityType, int entityId, IReadOnlyList<int> attachmentIds,
        string requestingUserId, CancellationToken ct = default)
    {
        if (attachmentIds.Count == 0) return;
        foreach (var attachment in await LoadLinkableAsync(entityType, attachmentIds, requestingUserId, ct))
            attachment.EntityId = entityId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<FileAttachment>> LoadLinkableAsync(string entityType, IReadOnlyList<int> attachmentIds,
        string requestingUserId, CancellationToken ct)
    {
        if (attachmentIds.Count == 0) return [];
        var distinct = attachmentIds.Distinct().ToList();
        var attachments = await db.FileAttachments
            .Where(a => distinct.Contains(a.Id) && !a.IsDeleted)
            .ToListAsync(ct);

        if (attachments.Count != distinct.Count)
            throw new ValidationFailedException("One or more attachments could not be found.");

        foreach (var attachment in attachments)
        {
            if (!string.Equals(attachment.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
                throw new ValidationFailedException($"Attachment {attachment.Id} was not uploaded for a {entityType}.");
            if (attachment.EntityId is not null)
                throw new ValidationFailedException($"Attachment {attachment.Id} is already linked to another record.");
            if (attachment.UploadedByUserId != requestingUserId)
                throw new ValidationFailedException($"Attachment {attachment.Id} was not uploaded by you.");
        }
        return attachments;
    }

    public async Task<IReadOnlyList<FileAttachmentDto>> GetForEntityAsync(string entityType, int entityId,
        CancellationToken ct = default)
    {
        return await db.FileAttachments.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && !a.IsDeleted)
            .OrderBy(a => a.UploadedAtUtc)
            .Select(a => new FileAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenAsync(int id, CancellationToken ct = default)
    {
        var attachment = await db.FileAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FileAttachment), id);

        var stream = await fileStorage.OpenAsync(attachment.RelativePath, ct);
        return (stream, attachment.ContentType, attachment.FileName);
    }

    private static FileAttachmentDto ToDto(FileAttachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc);
}

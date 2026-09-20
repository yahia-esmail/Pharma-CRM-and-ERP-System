namespace PharmaERP.Application.Files;

public interface IFileAttachmentService
{
    /// <summary>Uploads a file and, if <paramref name="entityId"/> is null, leaves it unlinked until a
    /// later LinkAsync call (the entity may not exist yet at upload time).</summary>
    Task<FileAttachmentDto> UploadAsync(string entityType, int? entityId, string uploadedByUserId, string fileName,
        string contentType, long sizeBytes, Stream content, CancellationToken ct = default);

    /// <summary>Links previously-uploaded, still-unlinked attachments (owned by <paramref name="requestingUserId"/>)
    /// to the given entity — used once the parent record (e.g. an Expense) has been created.</summary>
    Task LinkAsync(string entityType, int entityId, IReadOnlyList<int> attachmentIds, string requestingUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<FileAttachmentDto>> GetForEntityAsync(string entityType, int entityId, CancellationToken ct = default);

    Task<(Stream Content, string ContentType, string FileName)> OpenAsync(int id, CancellationToken ct = default);
}

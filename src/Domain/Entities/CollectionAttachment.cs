using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Photo/scan proof of payment attached to a Collection (addendum 3.5) — cash receipt, cheque
/// image, bank transfer confirmation. Content lives on disk (see IFileStorageService); this row is just
/// the pointer + metadata.</summary>
public class CollectionAttachment : AuditableEntity
{
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string RelativePath { get; set; } = null!;
    public long SizeBytes { get; set; }

    public DateTime UploadedAtUtc { get; set; }
    public string UploadedByUserId { get; set; } = null!;
}

using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Generic file/photo attachment, linked to any entity via <see cref="EntityType"/>/<see cref="EntityId"/>
/// (mirrors how <see cref="Notification"/> generalizes across entities). Content lives on disk (see
/// IFileStorageService); this row is just the pointer + metadata. <see cref="EntityId"/> starts null so a
/// file can be uploaded before its parent record exists, then linked once the parent is created.</summary>
public class FileAttachment : AuditableEntity
{
    public string EntityType { get; set; } = null!;
    public int? EntityId { get; set; }

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string RelativePath { get; set; } = null!;
    public long SizeBytes { get; set; }

    public DateTime UploadedAtUtc { get; set; }
    public string UploadedByUserId { get; set; } = null!;
}

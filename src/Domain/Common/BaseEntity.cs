namespace PharmaERP.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }
}

public abstract class AuditableEntity : BaseEntity
{
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public string? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

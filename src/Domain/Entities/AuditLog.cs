using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Central, append-only log of every create/update/delete across the system (see spec 4.11).
/// Written exclusively by the SaveChanges interceptor in Infrastructure — never by application code directly.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public AuditAction Action { get; set; }
    public string? PerformedByUserId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
}

namespace PharmaERP.Domain.Entities;

/// <summary>
/// Optional periodic GPS ping during working hours for route verification (spec 4.10). Deliberately
/// not an AuditableEntity — high-volume, append-only telemetry, not a business transaction that needs
/// full audit-log tracing; still retained (soft cleanup only) for the manager map view (spec 4.2).
/// </summary>
public class LocationPing
{
    public long Id { get; set; }
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime TimestampUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

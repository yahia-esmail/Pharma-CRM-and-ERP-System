using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Remembers the outcome of a mutating API call made with an <c>Idempotency-Key</c> header, so the
/// mobile app's outbox can safely resend a request whose response was lost (weak mobile network) without
/// creating a second order, collection or visit. A record with no <see cref="CompletedAtUtc"/> is a request
/// still in progress. Records are purged after a retention window (see IdempotencyCleanupService).</summary>
public class IdempotencyRecord : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;

    /// <summary>SHA-256 of method + path + body — a reused key with a different request is rejected.</summary>
    public string RequestHash { get; set; } = null!;

    public int? StatusCode { get; set; }
    public string? ResponseContentType { get; set; }
    public string? ResponseLocation { get; set; }
    public byte[]? ResponseBody { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Server-side record of an issued JWT refresh token (spec 5.4's "short-lived access token +
/// refresh token" pattern) — only a SHA-256 hash of the token is stored, never the raw value, so a
/// database read can't be used to impersonate a session. Rotated on every use: redeeming one revokes it
/// and issues a successor, recorded in <see cref="ReplacedByTokenHash"/>. Presenting a rotated token again
/// shortly afterwards means the client never received the reply (grace window); later, it means the token
/// was copied, and the whole chain of successors is revoked (see TokenService).</summary>
public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Set when revoked by rotation (null when revoked by logout or reuse detection).</summary>
    public string? ReplacedByTokenHash { get; set; }
}

using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Server-side record of an issued JWT refresh token (spec 5.4's "short-lived access token +
/// refresh token" pattern) — only a SHA-256 hash of the token is stored, never the raw value, so a
/// database read can't be used to impersonate a session. Rotated on every use: redeeming one revokes it
/// and issues a new one, so a stolen-and-reused token is detectable (it'll already be revoked).</summary>
public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

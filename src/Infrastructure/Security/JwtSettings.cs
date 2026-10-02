namespace PharmaERP.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>How long a just-rotated refresh token is still honoured: the app may have been closed, or the
    /// connection dropped, before the reply carrying its successor arrived. Without it the rep is signed
    /// out on the next start (with work possibly still waiting in the outbox).</summary>
    public int RefreshReuseGraceSeconds { get; set; } = 120;
}

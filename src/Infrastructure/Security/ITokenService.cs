using PharmaERP.Infrastructure.Identity;

namespace PharmaERP.Infrastructure.Security;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public record RefreshRotation(string UserId, string NewRefreshToken);

public interface ITokenService
{
    Task<AccessToken> CreateAccessTokenAsync(ApplicationUser user);

    /// <summary>Issues a new refresh token, persisting only its hash, and returns the raw value to send
    /// to the client.</summary>
    Task<string> CreateRefreshTokenAsync(string userId, CancellationToken ct = default);

    /// <summary>Redeems a refresh token: revokes it and issues its successor in one step (rotation). Returns null
    /// when the session is over — unknown, expired, logged out, or a rotated token replayed after the grace
    /// window (then every session descended from it is revoked too). A token rotated within the grace window
    /// (JwtSettings.RefreshReuseGraceSeconds) is honoured once more: the client lost the previous reply.</summary>
    Task<RefreshRotation?> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes a refresh token if it exists and is still active — used for logout. A no-op for an
    /// already-revoked, expired, or unknown token, so it never reveals which case applied.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}

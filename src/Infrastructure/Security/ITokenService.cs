using PharmaERP.Infrastructure.Identity;

namespace PharmaERP.Infrastructure.Security;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    Task<AccessToken> CreateAccessTokenAsync(ApplicationUser user);

    /// <summary>Issues a new refresh token, persisting only its hash, and returns the raw value to send
    /// to the client.</summary>
    Task<string> CreateRefreshTokenAsync(string userId, CancellationToken ct = default);

    /// <summary>Validates a refresh token (not expired, not already revoked) and revokes it in the same
    /// step (rotation) — returns the owning user's id, or null if the token is invalid/expired/revoked.</summary>
    Task<string?> ValidateAndRevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes a refresh token if it exists and is still active — used for logout. A no-op for an
    /// already-revoked, expired, or unknown token, so it never reveals which case applied.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}

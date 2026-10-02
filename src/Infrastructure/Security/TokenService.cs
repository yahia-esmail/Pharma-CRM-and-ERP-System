using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PharmaERP.Domain.Entities;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Infrastructure.Security;

/// <summary>Issues short-lived JWT access tokens for the mobile app / API (spec 5.1, 5.4).</summary>
public class TokenService(UserManager<ApplicationUser> userManager, IOptions<JwtSettings> jwtOptions, ApplicationDbContext db,
    TimeProvider time) : ITokenService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    public async Task<AccessToken> CreateAccessTokenAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email!),
            new(ClaimTypes.Email, user.Email!),
            new("FullName", user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        if (user.RepresentativeId is { } repId)
        {
            claims.Add(new Claim("RepresentativeId", repId.ToString()));

            var territoryId = await db.Representatives
                .Where(r => r.Id == repId)
                .Select(r => r.TerritoryId)
                .FirstOrDefaultAsync();

            if (territoryId is not null)
                claims.Add(new Claim("TerritoryId", territoryId.ToString()!));
        }

        var key = new SymmetricSecurityKey(Convert.FromBase64String(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = Now.AddMinutes(_settings.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public async Task<string> CreateRefreshTokenAsync(string userId, CancellationToken ct = default)
    {
        var (raw, entity) = NewRefreshToken(userId);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshRotation?> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var utcNow = Now;
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.ExpiresAtUtc <= utcNow) return null;

        if (token.RevokedAtUtc is { } revokedAt)
        {
            // Revoked by logout or by reuse detection: the session is over.
            if (token.ReplacedByTokenHash is null) return null;

            if (utcNow - revokedAt > TimeSpan.FromSeconds(_settings.RefreshReuseGraceSeconds))
            {
                // Rotated long ago and presented again: the token was copied. End every session descended from it.
                await RevokeChainAsync(token.ReplacedByTokenHash, utcNow, ct);
                await db.SaveChangesAsync(ct);
                return null;
            }

            // Just rotated: the client never got the reply. Its successor was never received, so retire it and
            // issue a new one in its place — there is still exactly one live token in this chain.
            await RevokeChainAsync(token.ReplacedByTokenHash, utcNow, ct);
        }

        var (raw, successor) = NewRefreshToken(token.UserId);
        db.RefreshTokens.Add(successor);
        token.RevokedAtUtc ??= utcNow;
        token.ReplacedByTokenHash = successor.TokenHash;
        await db.SaveChangesAsync(ct);   // revoke + issue in one step
        return new RefreshRotation(token.UserId, raw);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var utcNow = Now;

        var token = await db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.RevokedAtUtc == null && t.ExpiresAtUtc > utcNow, ct);
        if (token is null) return;

        token.RevokedAtUtc = utcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Revokes the token with <paramref name="hash"/> and every successor issued after it.</summary>
    private async Task RevokeChainAsync(string? hash, DateTime utcNow, CancellationToken ct)
    {
        for (var guard = 0; hash is not null && guard < 1000; guard++)
        {
            var next = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (next is null) return;
            next.RevokedAtUtc ??= utcNow;
            hash = next.ReplacedByTokenHash;
        }
    }

    private (string Raw, RefreshToken Entity) NewRefreshToken(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var utcNow = Now;
        return (raw, new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.AddDays(_settings.RefreshTokenDays)
        });
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

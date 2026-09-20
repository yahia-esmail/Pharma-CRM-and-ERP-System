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
public class TokenService(UserManager<ApplicationUser> userManager, IOptions<JwtSettings> jwtOptions, ApplicationDbContext db)
    : ITokenService
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
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

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
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays)
        });
        await db.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task<string?> ValidateAndRevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var utcNow = DateTime.UtcNow;

        var token = await db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.RevokedAtUtc == null && t.ExpiresAtUtc > utcNow, ct);
        if (token is null) return null;

        token.RevokedAtUtc = utcNow;
        await db.SaveChangesAsync(ct);

        return token.UserId;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var utcNow = DateTime.UtcNow;

        var token = await db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.RevokedAtUtc == null && t.ExpiresAtUtc > utcNow, ct);
        if (token is null) return;

        token.RevokedAtUtc = utcNow;
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

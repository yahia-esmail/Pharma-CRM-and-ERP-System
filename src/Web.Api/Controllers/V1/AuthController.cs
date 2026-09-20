using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Infrastructure.Security;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.Web.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
    ITokenService tokenService) : ControllerBase
{
    /// <summary>Issues a JWT access token for the mobile app (spec 5.1) — used by field representatives.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials." });

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials." });

        var accessToken = await tokenService.CreateAccessTokenAsync(user);
        var refreshToken = await tokenService.CreateRefreshTokenAsync(user.Id, ct);
        var roles = await userManager.GetRolesAsync(user);

        return Ok(new LoginResponse(accessToken.Token, accessToken.ExpiresAtUtc, refreshToken, user.FullName, roles.ToList()));
    }

    /// <summary>Redeems a refresh token for a new access token, rotating it in the same step (spec 5.4) —
    /// lets the mobile app renew a session without forcing the user to log in again.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var userId = await tokenService.ValidateAndRevokeRefreshTokenAsync(request.RefreshToken, ct);
        if (userId is null)
            return Unauthorized(new ProblemDetails { Title = "Invalid or expired refresh token." });

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Title = "Invalid or expired refresh token." });

        var accessToken = await tokenService.CreateAccessTokenAsync(user);
        var newRefreshToken = await tokenService.CreateRefreshTokenAsync(user.Id, ct);
        var roles = await userManager.GetRolesAsync(user);

        return Ok(new LoginResponse(accessToken.Token, accessToken.ExpiresAtUtc, newRefreshToken, user.FullName, roles.ToList()));
    }

    /// <summary>Revokes a refresh token so it can no longer be redeemed — the mobile app calls this on sign-out.</summary>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }
}

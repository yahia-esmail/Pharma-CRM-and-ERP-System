using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Infrastructure.Security;
using PharmaERP.Web.Api.Contracts;
using PharmaERP.Web.Api.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
    ITokenService tokenService, IPasskeyHandler<ApplicationUser> passkeys, PasskeyFlows flows) : ControllerBase
{
    /// <summary>Issues a JWT access token for the mobile app (spec 5.1) — used by field representatives.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.SignIn)]
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

    /// <summary>Starts a passkey sign-in (fingerprint / face unlock). No user name: the phone offers the passkeys it
    /// holds for this app.</summary>
    [HttpPost("passkey/options")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<ActionResult<PasskeyOptionsResponse>> PasskeyOptions()
    {
        var options = await passkeys.MakeRequestOptionsAsync(null, HttpContext);
        return Ok(new PasskeyOptionsResponse(flows.Start(PasskeyFlows.Kind.SignIn, null, options.AssertionState), options.RequestOptionsJson));
    }

    /// <summary>Completes a passkey sign-in and issues the same tokens as a password sign-in.</summary>
    [HttpPost("passkey")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<ActionResult<LoginResponse>> PasskeySignIn(PasskeyCompleteRequest request, CancellationToken ct)
    {
        if (flows.Take(request.FlowId, PasskeyFlows.Kind.SignIn, null) is not { } state)
            return Unauthorized(new ProblemDetails { Title = "This sign-in expired — try again." });

        var result = await passkeys.PerformAssertionAsync(new PasskeyAssertionContext
        {
            CredentialJson = request.CredentialJson,
            AssertionState = state,
            HttpContext = HttpContext
        });
        if (!result.Succeeded || result.User is not { IsActive: true } user)
            return Unauthorized(new ProblemDetails { Title = "Fingerprint sign-in failed." });
        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized(new ProblemDetails { Title = "This account is locked." });

        await userManager.AddOrUpdatePasskeyAsync(user, result.Passkey);   // new signature counter (clone detection)
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
        var rotation = await tokenService.RotateRefreshTokenAsync(request.RefreshToken, ct);
        if (rotation is null)
            return Unauthorized(new ProblemDetails { Title = "Invalid or expired refresh token." });

        var user = await userManager.FindByIdAsync(rotation.UserId);
        if (user is null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Title = "Invalid or expired refresh token." });

        var accessToken = await tokenService.CreateAccessTokenAsync(user);
        var roles = await userManager.GetRolesAsync(user);

        return Ok(new LoginResponse(accessToken.Token, accessToken.ExpiresAtUtc, rotation.NewRefreshToken, user.FullName, roles.ToList()));
    }

    /// <summary>Revokes a refresh token so it can no longer be redeemed — the mobile app calls this on sign-out.</summary>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }
}

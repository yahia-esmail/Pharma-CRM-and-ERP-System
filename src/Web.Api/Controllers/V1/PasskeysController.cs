using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Web.Api.Contracts;
using PharmaERP.Web.Api.Security;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>The signed-in user's passkeys (plan 9.3, wireframe 1 "Biometric Sign In"): register one for this phone's
/// fingerprint / face unlock, list, remove. Signing in with one is in AuthController.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PasskeysController(UserManager<ApplicationUser> users, IPasskeyHandler<ApplicationUser> passkeys,
    PasskeyFlows flows, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PasskeyDto>>> GetMine()
    {
        if (await CurrentUserAsync() is not { } user) return Forbid();
        return Ok((await users.GetPasskeysAsync(user))
            .Select(p => new PasskeyDto(Base64Url(p.CredentialId), p.Name, p.CreatedAt)).ToList());
    }

    [HttpPost("registration/options")]
    public async Task<ActionResult<PasskeyOptionsResponse>> RegistrationOptions()
    {
        if (await CurrentUserAsync() is not { } user) return Forbid();
        var options = await passkeys.MakeCreationOptionsAsync(
            new PasskeyUserEntity { Id = user.Id, Name = user.Email ?? user.UserName!, DisplayName = user.FullName }, HttpContext);
        return Ok(new PasskeyOptionsResponse(flows.Start(PasskeyFlows.Kind.Registration, user.Id, options.AttestationState),
            options.CreationOptionsJson));
    }

    [HttpPost("registration")]
    public async Task<ActionResult> Register(PasskeyCompleteRequest request)
    {
        if (await CurrentUserAsync() is not { } user) return Forbid();
        if (flows.Take(request.FlowId, PasskeyFlows.Kind.Registration, user.Id) is not { } state)
            return BadRequest(new ProblemDetails { Title = "This passkey setup expired — start again." });

        var result = await passkeys.PerformAttestationAsync(new PasskeyAttestationContext
        {
            CredentialJson = request.CredentialJson,
            AttestationState = state,
            HttpContext = HttpContext
        });
        if (!result.Succeeded)
            return BadRequest(new ProblemDetails { Title = "The passkey couldn't be verified.", Detail = result.Failure?.Message });

        var passkey = result.Passkey;
        var name = string.IsNullOrWhiteSpace(request.Name) ? "This phone" : request.Name.Trim();
        passkey.Name = name.Length > 60 ? name[..60] : name;
        var saved = await users.AddOrUpdatePasskeyAsync(user, passkey);
        return saved.Succeeded
            ? NoContent()
            : BadRequest(new ProblemDetails { Title = string.Join(" ", saved.Errors.Select(e => e.Description)) });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Remove(string id)
    {
        if (await CurrentUserAsync() is not { } user) return Forbid();
        byte[] credentialId;
        try { credentialId = FromBase64Url(id); }
        catch (FormatException) { return NotFound(); }
        await users.RemovePasskeyAsync(user, credentialId);
        return NoContent();
    }

    private async Task<ApplicationUser?> CurrentUserAsync() =>
        currentUser.UserId is { } id && await users.FindByIdAsync(id) is { IsActive: true } user ? user : null;

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }
}

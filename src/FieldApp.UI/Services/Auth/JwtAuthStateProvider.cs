using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>Signed in == a stored session exists. Identity comes from the persisted login response
/// rather than from parsing the access token, so the app still opens (offline) after a restart,
/// when no access token has been issued yet.</summary>
public sealed class JwtAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokens;

    public JwtAuthStateProvider(TokenStore tokens)
    {
        _tokens = tokens;
        _tokens.SessionChanged += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _tokens.InitializeAsync();
        if (_tokens.Session is not { } session) return Anonymous;

        var claims = new List<Claim> { new(ClaimTypes.Name, session.FullName) };
        claims.AddRange(session.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "PharmaJwt")));
    }

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _tokens.SessionChanged -= OnSessionChanged;
}

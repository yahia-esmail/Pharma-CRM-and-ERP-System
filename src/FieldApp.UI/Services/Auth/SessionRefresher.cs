using PharmaERP.FieldApp.UI.Services.Api;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>Single-flight token refresh: however many requests hit an expired token at once, only one
/// Auth/refresh call goes out — the API rotates refresh tokens, so a second concurrent call would
/// present an already-revoked token and sign the user out.
///
/// A reply lost in transit (app closed, signal dropped) is covered by the API: the just-rotated token is
/// honoured again for a short grace window (Jwt:RefreshReuseGraceSeconds), so the rep isn't signed out.
///
/// TODO(phase 11): this lock only covers one tab. Two installed windows (or a future service-worker
/// sync) need navigator.locks.</summary>
public sealed class SessionRefresher(TokenStore tokens, AuthApi authApi)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<AuthCallStatus> RefreshAsync(CancellationToken ct = default)
    {
        var tokenBefore = tokens.AccessToken;
        await _lock.WaitAsync(ct);
        try
        {
            if (tokens.AccessToken != tokenBefore && tokens.AccessToken is not null)
                return AuthCallStatus.Succeeded;   // another caller refreshed while we waited

            await tokens.InitializeAsync();
            if (tokens.Session is null) return AuthCallStatus.Rejected;

            var result = await authApi.RefreshAsync(tokens.Session.RefreshToken, ct);
            switch (result.Status)
            {
                case AuthCallStatus.Succeeded:
                    await tokens.SaveAsync(result.Response!);
                    break;
                case AuthCallStatus.Rejected:
                    // Revoked or expired: the session is really over. Being offline is NOT this case —
                    // the rep keeps working from local data until the network returns.
                    await tokens.ClearAsync();
                    break;
            }
            return result.Status;
        }
        finally { _lock.Release(); }
    }
}

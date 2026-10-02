using PharmaERP.FieldApp.UI.Services.Storage;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>What survives an app restart: the refresh token plus enough identity to render the app
/// offline. The access token is deliberately not persisted (plan 9.2) — it is re-issued on start.</summary>
public sealed record StoredSession(string RefreshToken, string FullName, IReadOnlyList<string> Roles);

/// <summary>Singleton holder of the current tokens. Registered as a singleton (not scoped) because
/// IHttpClientFactory builds message handlers in their own DI scope — a scoped store would give
/// AuthHeaderHandler a different, empty instance.</summary>
public sealed class TokenStore(KeyValueStore storage)
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public string? AccessToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public StoredSession? Session { get; private set; }

    public event Action? SessionChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            Session = await storage.GetAsync<StoredSession>(StorageKeys.Session);
            _initialized = true;
        }
        finally { _initLock.Release(); }
    }

    public async Task SaveAsync(LoginResponse response)
    {
        var hadSession = Session is not null;
        AccessToken = response.AccessToken;
        AccessTokenExpiresAtUtc = DateTime.SpecifyKind(response.ExpiresAtUtc, DateTimeKind.Utc);
        Session = new StoredSession(response.RefreshToken, response.FullName, response.Roles);
        _initialized = true;

        await storage.SetAsync(StorageKeys.Session, Session);
        if (!hadSession) SessionChanged?.Invoke();
    }

    public async Task ClearAsync()
    {
        var hadSession = Session is not null;
        AccessToken = null;
        AccessTokenExpiresAtUtc = default;
        Session = null;

        await storage.RemoveAsync(StorageKeys.Session);
        if (hadSession) SessionChanged?.Invoke();
    }

    public bool AccessTokenNeedsRefresh(TimeSpan margin) =>
        AccessToken is null || AccessTokenExpiresAtUtc - DateTime.UtcNow < margin;
}

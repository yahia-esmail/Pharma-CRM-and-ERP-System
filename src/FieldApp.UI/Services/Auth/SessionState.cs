using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Storage;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>The signed-in rep's profile (Users/me): representative id, territory, manager. Served from
/// the local cache first so screens render offline, then refreshed from the API when reachable.</summary>
public sealed class SessionState(UsersApi usersApi, KeyValueStore storage)
{
    public UserProfileDto? Profile { get; private set; }

    public event Action? Changed;

    public async Task<UserProfileDto?> EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (Profile is not null) return Profile;

        Profile = await storage.GetAsync<UserProfileDto>(StorageKeys.Profile);
        if (Profile is null) await RefreshProfileAsync(ct);
        else Changed?.Invoke();
        return Profile;
    }

    public async Task RefreshProfileAsync(CancellationToken ct = default)
    {
        try
        {
            if (await usersApi.GetMeAsync(ct) is not { } profile) return;
            Profile = profile;
            await storage.SetAsync(StorageKeys.Profile, profile);
            Changed?.Invoke();
        }
        catch (HttpRequestException)
        {
            // Offline or API down: keep whatever was cached.
        }
    }

    public void Reset()
    {
        Profile = null;
        Changed?.Invoke();
    }
}

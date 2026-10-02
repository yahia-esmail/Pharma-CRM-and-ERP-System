using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Orders;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Services.Storage;
using PharmaERP.FieldApp.UI.Services.Visits;
using PharmaERP.Shared.Security;

namespace PharmaERP.FieldApp.UI.Services.Auth;

public enum LoginOutcome { Succeeded, InvalidCredentials, NotARepresentative, Unreachable }

public sealed class AuthService(
    AuthApi authApi,
    TokenStore tokens,
    SessionState session,
    KeyValueStore storage,
    Outbox outbox,
    SyncLoop syncLoop,
    LocationTrackerRunner tracking,
    TodayPlanState plan,
    VisitSessionManager visits,
    OrderEntryManager orders)
{
    public async Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await authApi.LoginAsync(email.Trim(), password, ct);
        switch (result.Status)
        {
            case AuthCallStatus.Rejected: return LoginOutcome.InvalidCredentials;
            case AuthCallStatus.Unreachable: return LoginOutcome.Unreachable;
        }

        var login = result.Response!;
        if (!login.Roles.Contains(Roles.Representative))
        {
            // Managers and back-office staff use Web.Mvc; don't leave a live refresh token behind.
            await authApi.LogoutAsync(login.RefreshToken, ct);
            return LoginOutcome.NotARepresentative;
        }

        await tokens.SaveAsync(login);
        await storage.RequestPersistenceAsync();
        await session.RefreshProfileAsync(ct);
        return LoginOutcome.Succeeded;
    }

    /// <summary>How many operations would be lost by signing out now (queued writes plus order drafts still on
    /// the phone) — the UI must warn when non-zero.</summary>
    public async Task<int> CountUnsyncedAsync() =>
        (await outbox.GetSummaryAsync()).Total + (await orders.GetDraftsAsync()).Count;

    /// <summary>Clears every local trace of the session, including the outbox. Callers check
    /// <see cref="CountUnsyncedAsync"/> first and get the rep's explicit confirmation if anything is pending.</summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await tracking.StopAsync();
        await syncLoop.StopAsync();

        if (tokens.Session is { } current)
            await authApi.LogoutAsync(current.RefreshToken, ct);

        session.Reset();
        plan.Reset();
        visits.Reset();
        orders.Reset();
        await storage.ClearAsync();
        await tokens.ClearAsync();
        outbox.NotifyChanged();
    }
}

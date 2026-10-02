using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Collections;
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
    OrderEntryManager orders,
    CollectionEntryManager collections,
    Requests.ReturnEntryManager returns,
    Requests.ExpenseEntryManager expenses,
    Notifications.NotificationCenter notifications,
    Notifications.PushService push,
    PasskeyService passkeys)
{
    public async Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await authApi.LoginAsync(email.Trim(), password, ct);
        switch (result.Status)
        {
            case AuthCallStatus.Rejected: return LoginOutcome.InvalidCredentials;
            case AuthCallStatus.Unreachable: return LoginOutcome.Unreachable;
        }

        return await CompleteSignInAsync(result.Response!, ct);
    }

    /// <summary>Fingerprint / face sign-in with this phone's passkey (plan 9.3).</summary>
    public async Task<PasskeyOutcome> LoginWithPasskeyAsync(CancellationToken ct = default)
    {
        var (outcome, login) = await passkeys.SignInAsync(ct);
        if (outcome != PasskeyOutcome.Succeeded || login is null) return outcome;
        return await CompleteSignInAsync(login, ct) switch
        {
            LoginOutcome.Succeeded => PasskeyOutcome.Succeeded,
            LoginOutcome.NotARepresentative => PasskeyOutcome.NotARepresentative,
            _ => PasskeyOutcome.Rejected
        };
    }

    private async Task<LoginOutcome> CompleteSignInAsync(PharmaERP.Web.Api.Contracts.LoginResponse login, CancellationToken ct)
    {
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
        // Before the tokens go: this phone must stop receiving the signed-out rep's notifications.
        await push.DisableAsync();

        if (tokens.Session is { } current)
            await authApi.LogoutAsync(current.RefreshToken, ct);

        session.Reset();
        plan.Reset();
        visits.Reset();
        orders.Reset();
        collections.Reset();
        returns.Reset();
        expenses.Reset();
        notifications.Reset();
        await storage.ClearAsync();
        await tokens.ClearAsync();
        outbox.NotifyChanged();
    }
}

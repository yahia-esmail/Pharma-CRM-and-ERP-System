using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace PharmaERP.Web.Api.Security;

/// <summary>Server-side state of a WebAuthn ceremony between its "options" and "complete" calls: the challenge
/// never travels to the client, expires after 5 minutes, and can be used once. A registration flow is also bound
/// to the user who started it.</summary>
public sealed class PasskeyFlows(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public enum Kind { Registration, SignIn }

    private sealed record Flow(Kind Kind, string? UserId, string? State);

    public string Start(Kind kind, string? userId, string? state)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        cache.Set(Key(id), new Flow(kind, userId, state), Lifetime);
        return id;
    }

    /// <summary>The flow's state, removed so it can't be replayed; null if unknown, expired, or not this caller's.</summary>
    public string? Take(string flowId, Kind kind, string? userId)
    {
        if (!cache.TryGetValue(Key(flowId), out Flow? flow) || flow is null) return null;
        cache.Remove(Key(flowId));
        return flow.Kind == kind && flow.UserId == userId ? flow.State ?? "" : null;
    }

    private static string Key(string id) => $"passkey-flow:{id}";
}

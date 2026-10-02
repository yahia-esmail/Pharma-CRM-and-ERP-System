using System.Net;
using System.Net.Http.Headers;
using PharmaERP.FieldApp.UI.Services.Api;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>Attaches the Bearer token, refreshes it shortly before expiry, and retries once on 401.</summary>
public sealed class AuthHeaderHandler(TokenStore tokens, SessionRefresher refresher) : DelegatingHandler
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await tokens.InitializeAsync();
        if (tokens.Session is not null && tokens.AccessTokenNeedsRefresh(RefreshMargin))
            await refresher.RefreshAsync(ct);

        Attach(request);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || tokens.Session is null)
            return response;

        if (await refresher.RefreshAsync(ct) != AuthCallStatus.Succeeded)
            return response;

        response.Dispose();
        using var retry = Clone(request);
        Attach(retry);
        return await base.SendAsync(retry, ct);
    }

    private void Attach(HttpRequestMessage request)
    {
        if (tokens.AccessToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // An HttpRequestMessage cannot be sent twice. Buffered content (JSON/string/bytes — everything the
    // API clients send) can be re-serialized, so it is reused as-is.
    private static HttpRequestMessage Clone(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = original.Content,
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in original.Options)
            clone.Options.TryAdd(option.Key, option.Value);
        return clone;
    }
}

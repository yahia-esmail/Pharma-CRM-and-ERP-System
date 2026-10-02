using System.Net;
using System.Net.Http.Json;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Services.Api;

public enum AuthCallStatus { Succeeded, Rejected, Unreachable }

public sealed record AuthCallResult(AuthCallStatus Status, LoginResponse? Response = null);

/// <summary>Auth/* endpoints. Uses the anonymous HttpClient (no AuthHeaderHandler), otherwise a refresh
/// triggered by the handler would recurse back into itself.</summary>
public sealed class AuthApi(IHttpClientFactory httpClientFactory)
{
    private HttpClient Http => httpClientFactory.CreateClient(HttpClientNames.Anonymous);

    public Task<AuthCallResult> LoginAsync(string email, string password, CancellationToken ct = default) =>
        SendAsync("api/v1/Auth/login", new LoginRequest(email, password), ct);

    public Task<AuthCallResult> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        SendAsync("api/v1/Auth/refresh", new RefreshRequest(refreshToken), ct);

    /// <summary>Best effort: a failed logout call must never keep the user signed in locally.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            using var _ = await Http.PostAsJsonAsync("api/v1/Auth/logout", new RefreshRequest(refreshToken), ApiJson.Options, ct);
        }
        catch (HttpRequestException) { }
    }

    private async Task<AuthCallResult> SendAsync<TRequest>(string url, TRequest body, CancellationToken ct)
    {
        try
        {
            using var response = await Http.PostAsJsonAsync(url, body, ApiJson.Options, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                return new(AuthCallStatus.Rejected);
            if (!response.IsSuccessStatusCode)
                return new(AuthCallStatus.Unreachable);

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options, ct);
            return login is null ? new(AuthCallStatus.Unreachable) : new(AuthCallStatus.Succeeded, login);
        }
        catch (HttpRequestException)
        {
            return new(AuthCallStatus.Unreachable);
        }
    }
}

public static class HttpClientNames
{
    public const string Anonymous = "PharmaApi.Anonymous";
}

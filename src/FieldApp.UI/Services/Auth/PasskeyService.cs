using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Storage;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Services.Auth;

/// <summary>The signed-in user's passkeys (Passkeys/* — needs the access token).</summary>
public sealed class PasskeysApi(HttpClient http)
{
    public async Task<IReadOnlyList<PasskeyDto>> GetMineAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PasskeyDto>>("api/v1/Passkeys", ApiJson.Options, ct) ?? [];

    public async Task<PasskeyOptionsResponse> RegistrationOptionsAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/v1/Passkeys/registration/options", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PasskeyOptionsResponse>(ApiJson.Options, ct))!;
    }

    /// <summary>Null on success, else the server's reason.</summary>
    public async Task<string?> RegisterAsync(PasskeyCompleteRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/Passkeys/registration", request, ApiJson.Options, ct);
        if (response.IsSuccessStatusCode) return null;
        try { return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("title").GetString(); }
        catch { return $"Setup failed ({(int)response.StatusCode})."; }
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/v1/Passkeys/{Uri.EscapeDataString(id)}", ct);
        response.EnsureSuccessStatusCode();
    }
}

public enum PasskeyOutcome { Succeeded, Cancelled, NotARepresentative, Rejected, Unreachable, Unsupported }

/// <summary>Fingerprint / face sign-in with passkeys (plan 9.3, wireframe 1 "Biometric Sign In"). Setting one up needs
/// signal (the server verifies it); afterwards it replaces typing the password on this phone.</summary>
public sealed class PasskeyService(IJSRuntime js, IHttpClientFactory httpClientFactory, PasskeysApi api, KeyValueStore storage)
    : IAsyncDisposable
{
    private const string EnrolledKey = "passkey:thisDevice";
    private const string DismissedKey = "passkey:promptDismissed";

    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module()
    {
        if (_module is null || _module.IsFaulted || _module.IsCanceled)
            _module = js.InvokeAsync<IJSObjectReference>("import", "./_content/PharmaERP.FieldApp.UI/js/passkey.js").AsTask();
        return _module;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try { return await (await Module()).InvokeAsync<bool>("available"); }
        catch (JSException) { return false; }
    }

    /// <summary>Whether to offer setting it up: the phone supports it, it isn't set up here, and the rep didn't say no.</summary>
    public async Task<bool> ShouldOfferAsync() =>
        !await storage.GetAsync<bool>(EnrolledKey) && !await storage.GetAsync<bool>(DismissedKey) && await IsAvailableAsync();

    public Task DismissOfferAsync() => storage.SetAsync(DismissedKey, true);

    /// <summary>Creates a passkey on this phone for the signed-in user. Null on success, else what to tell the rep.</summary>
    public async Task<string?> EnrollAsync(string deviceName)
    {
        try
        {
            var options = await api.RegistrationOptionsAsync();
            var created = await (await Module()).InvokeAsync<JsonElement>("create", options.OptionsJson);
            if (!created.GetProperty("ok").GetBoolean())
            {
                var reason = created.GetProperty("error").GetString();
                // Already set up here (e.g. before a sign-out wiped the phone's records): nothing more to offer.
                if (reason == "exists") await storage.SetAsync(EnrolledKey, true);
                return reason switch
                {
                    "cancelled" => "Setup was cancelled.",
                    "exists" => "This phone already has fingerprint sign-in for your account.",
                    var e => $"This phone couldn't create a passkey ({e})."
                };
            }
            var error = await api.RegisterAsync(new PasskeyCompleteRequest(options.FlowId, created.GetProperty("json").GetString()!, deviceName));
            if (error is null) await storage.SetAsync(EnrolledKey, true);
            return error;
        }
        catch (Exception ex) when (ex is HttpRequestException or JSException)
        {
            return "Couldn't set it up — check the connection and try again.";
        }
    }

    /// <summary>Signs in with a passkey on this phone. On success returns the tokens, exactly like a password sign-in.</summary>
    public async Task<(PasskeyOutcome Outcome, LoginResponse? Login)> SignInAsync(CancellationToken ct = default)
    {
        if (!await IsAvailableAsync()) return (PasskeyOutcome.Unsupported, null);
        var http = httpClientFactory.CreateClient(HttpClientNames.Anonymous);
        try
        {
            using var optionsResponse = await http.PostAsync("api/v1/Auth/passkey/options", null, ct);
            if (!optionsResponse.IsSuccessStatusCode) return (PasskeyOutcome.Unreachable, null);
            var options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyOptionsResponse>(ApiJson.Options, ct))!;

            var assertion = await (await Module()).InvokeAsync<JsonElement>("get", options.OptionsJson);
            if (!assertion.GetProperty("ok").GetBoolean())
                return (assertion.GetProperty("error").GetString() == "cancelled" ? PasskeyOutcome.Cancelled : PasskeyOutcome.Rejected, null);

            using var response = await http.PostAsJsonAsync("api/v1/Auth/passkey",
                new PasskeyCompleteRequest(options.FlowId, assertion.GetProperty("json").GetString()!), ApiJson.Options, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest) return (PasskeyOutcome.Rejected, null);
            if (!response.IsSuccessStatusCode) return (PasskeyOutcome.Unreachable, null);
            return (PasskeyOutcome.Succeeded, await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options, ct));
        }
        catch (HttpRequestException)
        {
            return (PasskeyOutcome.Unreachable, null);
        }
    }

    public Task<IReadOnlyList<PasskeyDto>> GetMineAsync() => api.GetMineAsync();

    public async Task RemoveAsync(string id, bool isThisDevice)
    {
        await api.RemoveAsync(id);
        if (isThisDevice) await storage.RemoveAsync(EnrolledKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true }) await _module.Result.DisposeAsync();
    }
}

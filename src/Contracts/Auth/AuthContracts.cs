using System.ComponentModel.DataAnnotations;

namespace PharmaERP.Web.Api.Contracts;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, string FullName, IReadOnlyList<string> Roles);

public record RefreshRequest([Required] string RefreshToken);

// ---- Passkeys (fingerprint / face sign-in, plan 9.3) -------------------------------------------------

/// <summary>WebAuthn options for the browser (PublicKeyCredentialCreationOptions / RequestOptions as JSON) and the
/// id of the server-side challenge they belong to.</summary>
public record PasskeyOptionsResponse(string FlowId, string OptionsJson);

/// <summary>The browser's credential (PublicKeyCredential serialized to JSON) for the flow started by the options call.</summary>
public record PasskeyCompleteRequest([Required] string FlowId, [Required] string CredentialJson, string? Name = null);

public record PasskeyDto(string Id, string? Name, DateTimeOffset CreatedAt);

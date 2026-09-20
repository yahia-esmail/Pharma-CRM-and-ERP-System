using System.ComponentModel.DataAnnotations;

namespace PharmaERP.Web.Api.Contracts;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, string FullName, IReadOnlyList<string> Roles);

public record RefreshRequest([Required] string RefreshToken);

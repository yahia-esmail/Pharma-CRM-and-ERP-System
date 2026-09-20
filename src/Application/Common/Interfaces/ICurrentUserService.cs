namespace PharmaERP.Application.Common.Interfaces;

/// <summary>
/// Abstracts the authenticated caller for both the MVC dashboard (cookie auth) and the Web API
/// (JWT auth), so Application-layer business rules and territory scoping (spec 4.9, 5.4) are
/// enforced identically regardless of which front end is calling.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    int? RepresentativeId { get; }
    int? TerritoryId { get; }
    bool IsInRole(string role);

    /// <summary>True for roles with unrestricted, cross-territory data access (Admin, Management, SalesManager).</summary>
    bool HasUnrestrictedAccess { get; }
}

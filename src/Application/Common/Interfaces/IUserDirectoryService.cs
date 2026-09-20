namespace PharmaERP.Application.Common.Interfaces;

/// <summary>Thin abstraction over ASP.NET Core Identity's role-membership lookup, so Application-layer
/// services (e.g. notification recipient resolution) never take a direct dependency on Infrastructure/Identity
/// — mirrors how ICurrentUserService already keeps the current-request identity out of Application.</summary>
public interface IUserDirectoryService
{
    /// <summary>Active users' ids currently in the given role.</summary>
    Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default);
}

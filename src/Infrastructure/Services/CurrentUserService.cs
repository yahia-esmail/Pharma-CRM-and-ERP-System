using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Security;

namespace PharmaERP.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => User?.Identity?.Name;

    public int? RepresentativeId
    {
        get
        {
            var value = User?.FindFirstValue("RepresentativeId");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public int? TerritoryId
    {
        get
        {
            var value = User?.FindFirstValue("TerritoryId");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public bool HasUnrestrictedAccess =>
        IsInRole(Roles.Admin) || IsInRole(Roles.Management) || IsInRole(Roles.SalesManager);
}

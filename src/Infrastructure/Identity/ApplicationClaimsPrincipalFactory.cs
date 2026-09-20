using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Infrastructure.Identity;

/// <summary>
/// Adds RepresentativeId/TerritoryId claims at sign-in so ICurrentUserService can scope queries
/// without a DB round-trip on every request (spec 4.9 territory-scoped authorization).
/// </summary>
public class ApplicationClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor,
    ApplicationDbContext dbContext)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        identity.AddClaim(new Claim("FullName", user.FullName));

        if (user.RepresentativeId is { } repId)
        {
            identity.AddClaim(new Claim("RepresentativeId", repId.ToString()));

            var territoryId = await dbContext.Representatives
                .Where(r => r.Id == repId)
                .Select(r => r.TerritoryId)
                .FirstOrDefaultAsync();

            if (territoryId is not null)
                identity.AddClaim(new Claim("TerritoryId", territoryId.ToString()!));
        }

        return principal;
    }
}

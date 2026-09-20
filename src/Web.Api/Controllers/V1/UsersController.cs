using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>The signed-in account's own profile for the mobile app's home screen (spec 6.5) — name,
/// employee code, territory, reporting manager, when the account is linked to a Representative record.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController(UserManager<ApplicationUser> userManager, IAppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var roles = await userManager.GetRolesAsync(user);

        string? employeeCode = null, territoryName = null, reportingManagerName = null;
        int? territoryId = null;

        if (user.RepresentativeId is { } repId)
        {
            var rep = await db.Representatives.AsNoTracking()
                .Include(r => r.Territory)
                .Include(r => r.ReportingManager)
                .FirstOrDefaultAsync(r => r.Id == repId, ct);

            employeeCode = rep?.EmployeeCode;
            territoryId = rep?.TerritoryId;
            territoryName = rep?.Territory?.Name;
            reportingManagerName = rep?.ReportingManager?.FullName;
        }

        return Ok(new UserProfileDto(user.Id, user.FullName, user.Email!, roles.ToList(), user.RepresentativeId,
            employeeCode, territoryId, territoryName, reportingManagerName));
    }
}

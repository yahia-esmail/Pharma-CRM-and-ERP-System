using Microsoft.AspNetCore.Identity;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Identity;

namespace PharmaERP.Infrastructure.Services;

public class UserDirectoryService(UserManager<ApplicationUser> userManager) : IUserDirectoryService
{
    public async Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default)
    {
        var users = await userManager.GetUsersInRoleAsync(role);
        return users.Where(u => u.IsActive).Select(u => u.Id).ToList();
    }
}

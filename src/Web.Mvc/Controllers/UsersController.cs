using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.UserManagement)]
public class UsersController(UserManager<ApplicationUser> userManager, IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        var list = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            list.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }
        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        var vm = new UserCreateViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName,
            RepresentativeId = vm.RepresentativeId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            await PopulateLookupsAsync(vm);
            return View(vm);
        }

        await userManager.AddToRoleAsync(user, vm.Role);
        TempData["StatusMessage"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);
        TempData["StatusMessage"] = user.IsActive ? "User activated." : "User deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLookupsAsync(UserCreateViewModel vm)
    {
        vm.AvailableRoles = Roles.All.Select(r => new SelectListItem(r, r));
        vm.Representatives = await db.Representatives.AsNoTracking()
            .OrderBy(r => r.FullName)
            .Select(r => new SelectListItem(r.FullName, r.Id.ToString()))
            .ToListAsync();
    }
}

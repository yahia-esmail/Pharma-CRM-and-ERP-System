using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Models;

namespace PharmaERP.Web.Mvc.Controllers;

public class HomeController(ICurrentUserService currentUser) : Controller
{
    public IActionResult Index()
    {
        // Admin/Management/SalesManager/DistrictManager land on the full Executive Summary (spec 4.8);
        // everyone else gets a lightweight welcome pointing at the module(s) relevant to their role.
        if (currentUser.HasUnrestrictedAccess || currentUser.IsInRole(Roles.DistrictManager))
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

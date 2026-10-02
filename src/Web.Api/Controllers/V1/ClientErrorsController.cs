using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Diagnostics;

namespace PharmaERP.Web.Api.Controllers.V1;

/// <summary>Errors reported by the field app (plan phase 11), written to the server log so field problems can be
/// diagnosed without the phone. Signed-in users only, rate-limited, and capped in size — it's a log feed, not storage.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ClientErrorsController(ILogger<ClientErrorsController> logger, ICurrentUserService currentUser) : ControllerBase
{
    private const int MaxEntries = 20;

    [HttpPost]
    [EnableRateLimiting(RateLimits.PerUser)]
    [RequestSizeLimit(64 * 1024)]
    public ActionResult Report(ClientErrorReport report)
    {
        foreach (var e in report.Errors.Take(MaxEntries))
        {
            logger.LogWarning("Field app {Level} for user {UserId} on {Path} (app {AppVersion}): [{Category}] {Message}{NewLine}{Exception}",
                Clip(e.Level, 20), currentUser.UserId, Clip(e.Path, 200), Clip(report.AppVersion, 40), Clip(e.Category, 200),
                Clip(e.Message, 1000), Environment.NewLine, Clip(e.Exception, 4000));
        }
        return Accepted();
    }

    private static string? Clip(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max] + "…";
}

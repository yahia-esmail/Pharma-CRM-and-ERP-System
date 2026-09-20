using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Shared.Security;
using PharmaERP.Web.Mvc.Infrastructure;

namespace PharmaERP.Web.Mvc.Controllers;

[Authorize(Policy = Policies.AuditTrailView)]
public class AuditTrailController(IAppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? entityType, string? entityId, int page = 1)
    {
        const int pageSize = 50;
        var query = db.AuditLogs.AsNoTracking().OrderByDescending(a => a.TimestampUtc).AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);

        ViewBag.EntityType = entityType;
        ViewBag.EntityId = entityId;
        ViewBag.Page = page;
        ViewBag.HasMore = await query.Skip(page * pageSize).AnyAsync();

        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(logs);
    }

    public async Task<IActionResult> Export(string? entityType, string? entityId)
    {
        var query = db.AuditLogs.AsNoTracking().OrderByDescending(a => a.TimestampUtc).AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);

        var logs = await query.Take(5000).ToListAsync();

        var csv = CsvExport.Build(
            ["Timestamp (UTC)", "Entity Type", "Entity Id", "Action", "Performed By", "IP Address"],
            logs.Select(l => new object?[]
            {
                l.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"), l.EntityType, l.EntityId, l.Action,
                l.PerformedByUserId, l.IpAddress
            }));

        return File(csv, "text/csv", $"audit-trail-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }
}

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Pilot;
using PharmaERP.Shared.Security;

namespace PharmaERP.Web.Mvc.Controllers;

/// <summary>Field pilot report (plan phase 12): how the field app performs per representative — GPS accuracy and
/// time to a fix against the plan's targets, flagged visits, visits left open, tracking volume. Used to judge the
/// pilot week and to tune the geofence radius and accuracy thresholds before the full rollout.</summary>
[Authorize(Policy = Policies.LocationView)]
public class PilotReportController(IPilotReportService reports, IAppDbContext db, IBusinessCalendar calendar) : Controller
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? territoryId, CancellationToken ct)
    {
        var (start, end) = Range(from, to);
        ViewBag.Territories = new SelectList(await db.Territories.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name }).ToListAsync(ct), "Id", "Name", territoryId);
        ViewBag.TerritoryId = territoryId;
        return View(await reports.GetAsync(start, end, territoryId, ct));
    }

    public async Task<IActionResult> Csv(DateOnly? from, DateOnly? to, int? territoryId, CancellationToken ct)
    {
        var (start, end) = Range(from, to);
        var report = await reports.GetAsync(start, end, territoryId, ct);
        var csv = new StringBuilder();
        csv.AppendLine("Representative,Territory,Visits,Active days,With location %,Median accuracy m,P90 accuracy m,Within 30 m %," +
                       "Median fix s,Fix within 10 s %,Location mismatch %,Outside territory %,Too short %,Clock suspect %," +
                       "Outside-geofence reasons,Left open,Avg duration min,Track points,Anomaly %,Location proposals");
        foreach (var r in report.Rows.Append(report.Total))
            csv.AppendLine(string.Join(',', Cell(r.Name), Cell(r.Territory), r.Visits, r.ActiveDays, N(r.WithLocationPct), N(r.MedianAccuracyMeters),
                N(r.P90AccuracyMeters), N(r.Within30mPct), N(r.MedianFixSeconds), N(r.Within10sPct), N(r.MismatchPct), N(r.OutsideTerritoryPct),
                N(r.TooShortPct), N(r.ClockSuspectPct), r.OutsideGeofenceReasons, r.LeftOpen, N(r.AverageDurationMinutes), r.TrackPoints,
                N(r.AnomalyPct), r.LocationProposals));
        // BOM so Excel opens Arabic names correctly.
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv",
            $"field-pilot-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    /// <summary>Defaults to the last 7 business days (the pilot week).</summary>
    private (DateOnly, DateOnly) Range(DateOnly? from, DateOnly? to)
    {
        var end = to ?? calendar.Today;
        return (from ?? end.AddDays(-6), end);
    }

    private static string N(double? v) => v?.ToString("0.#", CultureInfo.InvariantCulture) ?? "";

    private static string Cell(string? s) => s is null ? "" : s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}

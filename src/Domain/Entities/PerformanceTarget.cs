using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// A KPI target for one representative for one period (spec 4.2: "targets are set per period and
/// versioned, so historical performance is always evaluated against the target active at the time").
/// One row per (Representative, Year, Month, MetricKey) — never overwritten, only superseded by a new
/// row for a later period.
/// </summary>
public class PerformanceTarget : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>e.g. "VisitCoverage", "VisitFrequencyCompliance" — sales/collection targets join in later phases.</summary>
    public string MetricKey { get; set; } = null!;

    public decimal TargetValue { get; set; }
}

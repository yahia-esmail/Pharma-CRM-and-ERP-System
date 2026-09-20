namespace PharmaERP.Application.DistrictDashboard;

public record DistrictRepresentativeSummaryDto(
    int RepresentativeId,
    string RepresentativeName,
    int PlannedVisits,
    int ActualVisits,
    double VisitCoveragePercent,
    double VisitQualityPercent,
    decimal SalesThisMonth,
    decimal OutstandingCustody);

/// <summary>District level of the District -> Representative -> Customer -> Transaction drill-down
/// (addendum 3.11) — scoped strictly to one territory's subtree (see ITerritoryService's hierarchy
/// helper), never the wider org the way the Management Dashboard can be.</summary>
public record DistrictDashboardDto(
    int TerritoryId,
    string TerritoryName,
    int Year,
    int Month,
    decimal TotalSales,
    decimal TotalCollected,
    decimal TotalOutstandingCustody,
    double AverageVisitCoveragePercent,
    double AverageVisitQualityPercent,
    IReadOnlyList<DistrictRepresentativeSummaryDto> Representatives);

public record CustomerDrilldownDto(
    string Kind,
    int Id,
    string Name,
    int VisitsThisMonth,
    decimal? SalesThisMonth,
    decimal? OutstandingBalance);

/// <summary>Representative level of the drill-down — every doctor/pharmacy assigned to this rep, with
/// enough on each row to link onward to that customer's own page (the existing Doctor/Pharmacy Details
/// view already is the Customer -> Transaction level, right down to "Trace This Transaction").</summary>
public record RepresentativeDrilldownDto(
    int RepresentativeId,
    string RepresentativeName,
    string? TerritoryName,
    int Year,
    int Month,
    decimal SalesThisMonth,
    decimal OutstandingCustody,
    IReadOnlyList<CustomerDrilldownDto> Doctors,
    IReadOnlyList<CustomerDrilldownDto> Pharmacies);

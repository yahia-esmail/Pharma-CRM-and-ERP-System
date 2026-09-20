using System.ComponentModel.DataAnnotations;
using PharmaERP.Application.Performance;

namespace PharmaERP.Web.Mvc.Models;

public class PerformanceDashboardViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? TerritoryId { get; set; }
    public IReadOnlyList<PerformanceScorecardDto> Scorecards { get; set; } = [];
}

public class PerformanceTargetFormViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    [Required, Range(2020, 2100)]
    public int Year { get; set; } = DateTime.Today.Year;

    [Required, Range(1, 12)]
    public int Month { get; set; } = DateTime.Today.Month;

    [Required, Range(0, 100)]
    public decimal TargetValue { get; set; } = 90;
}

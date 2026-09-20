using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Web.Mvc.Models;

public class VisitPlanCreateViewModel
{
    [Required]
    public int RepresentativeId { get; set; }

    public VisitPlanPeriodType PeriodType { get; set; } = VisitPlanPeriodType.Weekly;

    [Required, DataType(DataType.Date)]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, DataType(DataType.Date)]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(6));

    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
}

public class VisitPlanAddItemViewModel
{
    public int VisitPlanId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required, DataType(DataType.Date)]
    public DateOnly PlannedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Sequence { get; set; }
    public string? Notes { get; set; }
}

public class VisitPlanRejectViewModel
{
    public int VisitPlanId { get; set; }

    [Required]
    public string Reason { get; set; } = null!;
}

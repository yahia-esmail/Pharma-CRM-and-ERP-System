using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Representative : AuditableEntity
{
    public string EmployeeCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public RepresentativeLevel Level { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;

    public int? ReportingManagerId { get; set; }
    public Representative? ReportingManager { get; set; }

    public int? TerritoryId { get; set; }
    public Territory? Territory { get; set; }

    /// <summary>Links this Representative record to its ASP.NET Core Identity login, if one has been provisioned.</summary>
    public string? ApplicationUserId { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    public ICollection<Representative> DirectReports { get; set; } = new List<Representative>();
    public ICollection<Doctor> PrimaryDoctors { get; set; } = new List<Doctor>();
}

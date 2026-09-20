using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Representatives;

public record RepresentativeListItemDto(
    int Id,
    string EmployeeCode,
    string FullName,
    RepresentativeLevel Level,
    string? TerritoryName,
    string? ReportingManagerName,
    EmploymentStatus EmploymentStatus);

public record RepresentativeDetailDto(
    int Id,
    string EmployeeCode,
    string FullName,
    RepresentativeLevel Level,
    EmploymentStatus EmploymentStatus,
    int? TerritoryId,
    string? TerritoryName,
    int? ReportingManagerId,
    string? ReportingManagerName,
    string? Phone,
    string? Email,
    string? ApplicationUserId);

public class RepresentativeSaveRequest
{
    public string EmployeeCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public RepresentativeLevel Level { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public int? TerritoryId { get; set; }
    public int? ReportingManagerId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

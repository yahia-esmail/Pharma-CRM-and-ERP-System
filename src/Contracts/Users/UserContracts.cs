namespace PharmaERP.Web.Api.Contracts;

public record UserProfileDto(
    string Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    int? RepresentativeId,
    string? EmployeeCode,
    int? TerritoryId,
    string? TerritoryName,
    string? ReportingManagerName);

using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Doctors;

public record DoctorListItemDto(
    int Id,
    string FullName,
    string Specialty,
    string? City,
    string? ClassificationName,
    string? PrimaryRepresentativeName,
    DoctorStatus Status);

public record DoctorDetailDto(
    int Id,
    string FullName,
    string Specialty,
    string? SubSpecialty,
    string? ClinicOrHospital,
    string? Address,
    string? Governorate,
    string? City,
    string? Phone,
    string? WhatsAppNumber,
    string? Email,
    int? ClassificationId,
    string? ClassificationName,
    string? PreferredVisitingDays,
    string? PreferredVisitingTimes,
    int? PrimaryRepresentativeId,
    string? PrimaryRepresentativeName,
    int? TerritoryId,
    string? TerritoryName,
    DoctorStatus Status,
    double? Latitude,
    double? Longitude);

public class DoctorSaveRequest
{
    public string FullName { get; set; } = null!;
    public string Specialty { get; set; } = null!;
    public string? SubSpecialty { get; set; }
    public string? ClinicOrHospital { get; set; }
    public string? Address { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }
    public int? ClassificationId { get; set; }
    public string? PreferredVisitingDays { get; set; }
    public string? PreferredVisitingTimes { get; set; }
    public int? PrimaryRepresentativeId { get; set; }
    public int? TerritoryId { get; set; }
    public DoctorStatus Status { get; set; } = DoctorStatus.Prospect;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public record DuplicateDoctorMatch(int Id, string FullName, string? Phone, string? ClinicOrHospital);

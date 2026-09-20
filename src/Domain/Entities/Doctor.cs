using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Doctor : AuditableEntity
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
    public DoctorClassification? Classification { get; set; }

    public string? PreferredVisitingDays { get; set; }
    public string? PreferredVisitingTimes { get; set; }

    public int? PrimaryRepresentativeId { get; set; }
    public Representative? PrimaryRepresentative { get; set; }

    public int? TerritoryId { get; set; }
    public Territory? Territory { get; set; }

    public DoctorStatus Status { get; set; } = DoctorStatus.Prospect;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ICollection<DoctorVisit> Visits { get; set; } = new List<DoctorVisit>();
    public ICollection<DoctorFollowUp> FollowUps { get; set; } = new List<DoctorFollowUp>();
}

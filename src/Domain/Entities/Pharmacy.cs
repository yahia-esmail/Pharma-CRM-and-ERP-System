using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Pharmacy : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? LicenseNumber { get; set; }
    public string? OwnerName { get; set; }

    public string? Address { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }

    /// <summary>Class/segment (spec 4.3), e.g. A/B/C — kept as a free-form tag rather than a configurable
    /// tier table like DoctorClassification, since the spec doesn't tie pharmacy segment to visit targets.</summary>
    public string? Segment { get; set; }

    public int PaymentTermDays { get; set; } = 30;

    /// <summary>0 = no credit limit enforced.</summary>
    public decimal CreditLimit { get; set; }

    public int? PrimaryRepresentativeId { get; set; }
    public Representative? PrimaryRepresentative { get; set; }

    public int? TerritoryId { get; set; }
    public Territory? Territory { get; set; }

    public PharmacyStatus Status { get; set; } = PharmacyStatus.Prospect;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

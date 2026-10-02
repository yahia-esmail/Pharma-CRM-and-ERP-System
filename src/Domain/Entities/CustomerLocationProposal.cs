using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>A GPS position for a doctor's clinic or a pharmacy, captured by a representative on site
/// (plan 7.9). Most customers were imported without coordinates, so the geographic data is completed from
/// the field: a customer's <i>first</i> location is applied at once when the fix is accurate enough
/// (<see cref="LocationProposalStatus.AutoApplied"/>); changing an existing location always waits for a
/// manager, since that is exactly what someone faking check-ins would want to do.</summary>
public class CustomerLocationProposal : BaseEntity
{
    /// <summary>Exactly one of <see cref="DoctorId"/> / <see cref="PharmacyId"/> is set.</summary>
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int? PharmacyId { get; set; }
    public Pharmacy? Pharmacy { get; set; }

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AccuracyMeters { get; set; }
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>The customer's coordinates when the proposal was made (null = had none) — shown to the
    /// reviewer, and lets them see how far the proposed point moves the customer.</summary>
    public double? PreviousLatitude { get; set; }
    public double? PreviousLongitude { get; set; }

    public string? Note { get; set; }

    public LocationProposalStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}

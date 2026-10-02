using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Order : AuditableEntity
{
    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime OrderDateUtc { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string? RejectionReason { get; set; }

    /// <summary>When the rep sent it for approval (server clock).</summary>
    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>The pharmacy visit the order was taken during, when entered from the field app.</summary>
    public int? PharmacyVisitId { get; set; }
    public PharmacyVisit? PharmacyVisit { get; set; }

    /// <summary>Where the rep was when submitting from the field app (plan phase 7 — Event Fix). Recorded for
    /// review, never used to block the order.</summary>
    public double? SubmitLatitude { get; set; }
    public double? SubmitLongitude { get; set; }
    public double? SubmitAccuracyMeters { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public Sale? Sale { get; set; }
}

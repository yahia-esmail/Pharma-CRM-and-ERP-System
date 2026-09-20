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

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public Sale? Sale { get; set; }
}

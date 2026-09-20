using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// One periodic physical count vs. system balance check for a representative's custody (spec 4.5). A
/// nonzero variance produces a paired <see cref="CustodyTransaction"/> adjustment so the ledger is
/// trued up — this record itself is just the audit trail of that event, not a live balance.
/// </summary>
public class StockReconciliation : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public DateTime ReconciliationDateUtc { get; set; }
    public int SystemBalance { get; set; }
    public int CountedBalance { get; set; }
    public int Variance => CountedBalance - SystemBalance;
    public string? Notes { get; set; }

    public int? AdjustmentCustodyTransactionId { get; set; }
    public CustodyTransaction? AdjustmentCustodyTransaction { get; set; }
}

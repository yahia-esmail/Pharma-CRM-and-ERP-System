using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>One periodic physical cash count vs. system balance check for a representative's Financial
/// Custody (gap-analysis addendum 3.6, mirroring the existing StockReconciliation for stock). The
/// outstanding balance itself is never edited directly — a nonzero, Approved variance is simply an
/// additional term CollectionService adds into its derived outstanding-balance formula, the same way a
/// stock reconciliation's variance becomes just another CustodyTransaction row summed into a balance.</summary>
public class FinancialReconciliation : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public DateTime ReconciliationDateUtc { get; set; }
    public decimal SystemBalance { get; set; }
    public decimal CountedBalance { get; set; }
    public decimal Variance => CountedBalance - SystemBalance;

    /// <summary>Required once Variance != 0 — the explanation for the difference.</summary>
    public string? Reason { get; set; }

    public FinancialReconciliationStatus Status { get; set; } = FinancialReconciliationStatus.Pending;

    public string RequestedByUserId { get; set; } = null!;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}

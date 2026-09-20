namespace PharmaERP.Domain.Enums;

/// <summary>Addendum 3.6 — unlike stock reconciliation's always-auto-post variance, a financial
/// reconciliation with a nonzero difference stays Pending until a manager approves it; only then does the
/// variance count toward the representative's outstanding balance. A zero-variance count auto-approves
/// (nothing to explain).</summary>
public enum FinancialReconciliationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

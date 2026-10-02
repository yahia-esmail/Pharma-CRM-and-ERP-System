namespace PharmaERP.Domain.Entities;

/// <summary>The initial trigger catalog (addendum 3.10). Plain strings by design — new triggers can be
/// added without a migration or an enum change.</summary>
public static class NotificationTypes
{
    public const string VisitPlanPendingApproval = "VisitPlanPendingApproval";
    public const string PlannedVisitNotLogged = "PlannedVisitNotLogged";
    public const string OrderAwaitingApproval = "OrderAwaitingApproval";
    public const string CollectionAwaitingReview = "CollectionAwaitingReview";
    public const string LowStock = "LowStock";
    public const string NearExpiry = "NearExpiry";
    public const string CustodyBalanceAging = "CustodyBalanceAging";
    public const string SupplierPaymentDue = "SupplierPaymentDue";
    public const string ExpenseAwaitingApproval = "ExpenseAwaitingApproval";

    // Decisions on what a representative sent in (field app, wireframe 6) — sent to that representative.
    public const string OrderApproved = "OrderApproved";
    public const string OrderRejected = "OrderRejected";
    public const string ExpenseApproved = "ExpenseApproved";
    public const string ExpenseRejected = "ExpenseRejected";
    public const string ReturnApproved = "ReturnApproved";
    public const string ReturnRejected = "ReturnRejected";
}

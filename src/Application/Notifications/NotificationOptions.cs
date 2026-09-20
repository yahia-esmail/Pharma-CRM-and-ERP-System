namespace PharmaERP.Application.Notifications;

/// <summary>Configurable thresholds for the background notification scan (addendum 3.10 — "extensible
/// via configuration"). Bound from the "Notifications" appsettings section.</summary>
public class NotificationOptions
{
    public int ScanIntervalMinutes { get; set; } = 60;

    /// <summary>How long a visit plan can sit Submitted before its district manager is notified.</summary>
    public int VisitPlanApprovalThresholdHours { get; set; } = 24;

    /// <summary>How many days a representative's outstanding custody balance can age before Finance/DM are notified.</summary>
    public int OverdueCustodyDays { get; set; } = 7;

    /// <summary>Matches the Management Dashboard's existing near-expiry window, for consistency.</summary>
    public int NearExpiryThresholdDays { get; set; } = 90;

    /// <summary>No due-date field exists anywhere on PurchaseOrder/SupplierPayment today — this
    /// approximates "payment due" as goods received this many days ago with the receipt still unpaid
    /// (a common net-terms convention), rather than reading a due date that doesn't exist in the schema.</summary>
    public int SupplierPaymentDueDays { get; set; } = 30;
}

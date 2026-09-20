namespace PharmaERP.Domain.Enums;

/// <summary>Draft -> Submitted -> Approved/Rejected -> Delivered, with Cancel available pre-approval
/// (gap-analysis addendum 3.4). A Sale is only created on Delivered — Approved reserves nothing physical
/// yet, it just clears the pharmacy's credit-limit check ahead of fulfillment.</summary>
public enum OrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Delivered = 4,
    Cancelled = 5
}

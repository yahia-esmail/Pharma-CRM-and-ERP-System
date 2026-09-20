namespace PharmaERP.Domain.Enums;

public enum SupplierStatus
{
    Active = 0,
    Inactive = 1
}

public enum PurchaseOrderStatus
{
    Draft = 0,
    Sent = 1,
    PartiallyReceived = 2,
    Received = 3,
    Closed = 4,
    Cancelled = 5
}

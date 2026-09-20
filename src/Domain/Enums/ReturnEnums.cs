namespace PharmaERP.Domain.Enums;

/// <summary>Which leg of the Returns module a request belongs to (addendum 3.8) — each has its own
/// downstream effect and its own approval, but shares the same request/approve workflow shape.</summary>
public enum ReturnFlowType
{
    /// <summary>Pharmacy/doctor hands unsold or damaged product back to the representative.</summary>
    CustomerToRepresentative = 0,

    /// <summary>Representative hands expired/damaged/excess stock back to the warehouse.</summary>
    RepresentativeToWarehouse = 1
}

public enum ReturnReason
{
    Expired = 0,
    Damaged = 1,
    Unsold = 2,
    Other = 3
}

public enum ReturnStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

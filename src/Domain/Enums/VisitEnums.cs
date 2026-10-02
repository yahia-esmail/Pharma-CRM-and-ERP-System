namespace PharmaERP.Domain.Enums;

/// <summary>Where a visit is in the mobile check-in → check-out flow. Completed is 0 so every visit recorded
/// before the flow existed (single-request save) reads as completed.</summary>
public enum VisitSessionStatus
{
    Completed = 0,
    /// <summary>Checked in, not checked out yet — the rep is at the customer now (or forgot to finish).</summary>
    Open = 1,
    Cancelled = 2
}

/// <summary>The doctor's interest in the products discussed, as judged by the rep (wireframe 7).</summary>
public enum VisitInterestLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum LocationProposalStatus
{
    Pending = 0,
    /// <summary>Applied without review: the customer had no location and the fix was accurate enough.</summary>
    AutoApplied = 1,
    Approved = 2,
    Rejected = 3
}

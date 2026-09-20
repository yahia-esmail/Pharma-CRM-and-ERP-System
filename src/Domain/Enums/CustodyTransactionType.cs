namespace PharmaERP.Domain.Enums;

/// <summary>
/// Every entry in a representative's stock custody ledger (spec 4.5). Quantity is signed — positive
/// for Received/AdjustmentIncrease/TransferIn, negative for everything else — so a balance is always
/// just SUM(Quantity), never a separately maintained/editable field.
/// </summary>
public enum CustodyTransactionType
{
    Received = 0,
    Sold = 1,
    Returned = 2,
    AdjustmentIncrease = 3,
    AdjustmentDecrease = 4,
    TransferIn = 5,
    TransferOut = 6,

    /// <summary>Pharmacy -> representative handover (addendum 3.8) — positive, increases custody balance.
    /// Distinct from <see cref="Returned"/>, which is representative -> warehouse and decreases it.</summary>
    CustomerReturn = 7
}

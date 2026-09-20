namespace PharmaERP.Domain.Enums;

/// <summary>Which kind of customer a list holds (gap-analysis addendum 3.1).</summary>
public enum CustomerListType
{
    DoctorList = 0,
    PharmacyList = 1,
    TerritoryList = 2
}

/// <summary>Static = a fixed, manually-picked set of CustomerListItem rows.
/// Dynamic = membership computed live from the Filter* fields, never materialized as rows.</summary>
public enum CustomerListMode
{
    Static = 0,
    Dynamic = 1
}

public enum CustomerListStatus
{
    Active = 0,
    Archived = 1
}

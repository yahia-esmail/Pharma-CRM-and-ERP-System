namespace PharmaERP.Shared.Security;

/// <summary>Initial role set (spec 4.9) — extensible via custom roles in a later phase.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Management = "Management";
    public const string SalesManager = "SalesManager";
    public const string DistrictManager = "DistrictManager";
    public const string Representative = "Representative";
    public const string Warehouse = "Warehouse";
    public const string Finance = "Finance";
    public const string Procurement = "Procurement";

    public static readonly string[] All =
    [
        Admin, Management, SalesManager, DistrictManager, Representative, Warehouse, Finance, Procurement
    ];
}

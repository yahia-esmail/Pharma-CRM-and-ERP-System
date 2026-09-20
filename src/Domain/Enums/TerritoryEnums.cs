namespace PharmaERP.Domain.Enums;

/// <summary>Territory hierarchy depth (gap-analysis addendum 3.2): Governorate → City → District → Area.</summary>
public enum TerritoryType
{
    Governorate = 0,
    City = 1,
    District = 2,
    Area = 3
}

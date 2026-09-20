using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>A named, redistributable working set of customers (addendum 3.1), independent of the
/// underlying Doctor/Pharmacy records themselves.</summary>
public class CustomerList : AuditableEntity
{
    public string Name { get; set; } = null!;
    public CustomerListType Type { get; set; }
    public CustomerListMode Mode { get; set; }
    public CustomerListStatus Status { get; set; } = CustomerListStatus.Active;

    public int? OwnerRepresentativeId { get; set; }
    public Representative? OwnerRepresentative { get; set; }

    /// <summary>Dynamic-mode membership rule (addendum 3.1: "all Class A doctors in Territory X") —
    /// evaluated live at read time, never materialized into CustomerListItem rows. Null/ignored for Static lists.</summary>
    public int? FilterTerritoryId { get; set; }
    public Territory? FilterTerritory { get; set; }
    public int? FilterClassificationId { get; set; }
    public DoctorClassification? FilterClassification { get; set; }
    public string? FilterSegment { get; set; }
    public bool FilterActiveOnly { get; set; } = true;

    public ICollection<CustomerListItem> Items { get; set; } = new List<CustomerListItem>();
}

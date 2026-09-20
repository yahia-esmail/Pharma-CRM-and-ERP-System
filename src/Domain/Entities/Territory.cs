using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Territory : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? Region { get; set; }
    public string? Description { get; set; }

    /// <summary>Depth in the Governorate → City → District → Area hierarchy (addendum 3.2).</summary>
    public TerritoryType Type { get; set; } = TerritoryType.District;

    public int? ParentTerritoryId { get; set; }
    public Territory? ParentTerritory { get; set; }
    public ICollection<Territory> ChildTerritories { get; set; } = new List<Territory>();

    public int? DistrictManagerId { get; set; }
    public Representative? DistrictManager { get; set; }

    public ICollection<Representative> Representatives { get; set; } = new List<Representative>();
    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
}

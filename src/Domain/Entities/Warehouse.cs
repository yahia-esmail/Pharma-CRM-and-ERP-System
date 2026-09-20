using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Warehouse : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public WarehouseType Type { get; set; } = WarehouseType.Main;

    /// <summary>Identity user id of the responsible user — a plain string, like every other user
    /// reference in this codebase (see spec 5.3), not a cross-layer FK into Infrastructure/Identity.</summary>
    public string? ResponsibleUserId { get; set; }
}

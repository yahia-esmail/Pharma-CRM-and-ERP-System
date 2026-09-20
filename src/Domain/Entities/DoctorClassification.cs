using PharmaERP.Domain.Common;

namespace PharmaERP.Domain.Entities;

/// <summary>Admin-configurable tier (e.g. A/B/C) driving visit frequency targets for classified doctors.</summary>
public class DoctorClassification : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int TargetVisitsPerMonth { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
}

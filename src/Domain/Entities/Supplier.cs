using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

public class Supplier : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public SupplierStatus Status { get; set; } = SupplierStatus.Active;
}

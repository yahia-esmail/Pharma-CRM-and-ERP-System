using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>An expense claim submitted from the field (addendum 3.9). The submitter is the entity's
/// CreatedByUserId (auto-stamped by AuditSaveChangesInterceptor) — no separate "employee" column needed.</summary>
public class Expense : AuditableEntity
{
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }

    public int TerritoryId { get; set; }
    public Territory Territory { get; set; } = null!;

    public string? Description { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;

    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public string? ReimbursedByUserId { get; set; }
    public DateTime? ReimbursedAtUtc { get; set; }
}

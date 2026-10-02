using PharmaERP.Application.Files;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Expenses;

public record ExpenseDto(
    int Id,
    ExpenseType Type,
    decimal Amount,
    DateOnly ExpenseDate,
    int TerritoryId,
    string TerritoryName,
    string? Description,
    ExpenseStatus Status,
    string? RejectionReason,
    IReadOnlyList<FileAttachmentDto> Attachments);

public class ExpenseSaveRequest
{
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public int TerritoryId { get; set; }
    public string? Description { get; set; }
    public List<int> AttachmentIds { get; set; } = [];

    /// <summary>Send for approval in the same request, so the field app's outbox never leaves a half-done expense.</summary>
    public bool Submit { get; set; }
}

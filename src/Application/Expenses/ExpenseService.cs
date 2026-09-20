using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Files;
using PharmaERP.Application.Notifications;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Application.Expenses;

public class ExpenseService(IAppDbContext db, IFileAttachmentService fileAttachments,
    INotificationService notificationService, IUserDirectoryService userDirectory) : IExpenseService
{
    public async Task<PagedResult<ExpenseDto>> GetMineAsync(string userId, ExpenseStatus? status, PagedRequest request,
        CancellationToken ct = default)
    {
        var query = db.Expenses.AsNoTracking().Where(e => e.CreatedByUserId == userId);
        if (status.HasValue) query = query.Where(e => e.Status == status);

        var totalCount = await query.CountAsync(ct);

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new { e.Id, e.Type, e.Amount, e.ExpenseDate, e.TerritoryId, TerritoryName = e.Territory.Name,
                e.Description, e.Status, e.RejectionReason })
            .ToListAsync(ct);

        var items = new List<ExpenseDto>(expenses.Count);
        foreach (var e in expenses)
        {
            var attachments = await fileAttachments.GetForEntityAsync("Expense", e.Id, ct);
            items.Add(new ExpenseDto(e.Id, e.Type, e.Amount, e.ExpenseDate, e.TerritoryId, e.TerritoryName,
                e.Description, e.Status, e.RejectionReason, attachments));
        }

        return new PagedResult<ExpenseDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(string userId, ExpenseSaveRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ValidationFailedException("Amount must be greater than zero.");

        var territoryExists = await db.Territories.AnyAsync(t => t.Id == request.TerritoryId && !t.IsDeleted, ct);
        if (!territoryExists) throw new NotFoundException(nameof(Territory), request.TerritoryId);

        var expense = new Expense
        {
            Type = request.Type,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            TerritoryId = request.TerritoryId,
            Description = request.Description,
            Status = ExpenseStatus.Draft
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        await fileAttachments.LinkAsync("Expense", expense.Id, request.AttachmentIds, userId, ct);

        return expense.Id;
    }

    public async Task SubmitAsync(int expenseId, string userId, CancellationToken ct = default)
    {
        var expense = await LoadOwnedAsync(expenseId, userId, ct);

        if (expense.Status is not (ExpenseStatus.Draft or ExpenseStatus.Rejected))
            throw new ValidationFailedException("Only a draft or rejected expense can be submitted.");

        expense.Status = ExpenseStatus.Submitted;
        expense.RejectionReason = null;
        await db.SaveChangesAsync(ct);

        var recipientUserIds = new HashSet<string>();
        recipientUserIds.UnionWith(await userDirectory.GetUserIdsInRoleAsync(Roles.Management, ct));
        recipientUserIds.UnionWith(await userDirectory.GetUserIdsInRoleAsync(Roles.SalesManager, ct));
        recipientUserIds.UnionWith(await userDirectory.GetUserIdsInRoleAsync(Roles.DistrictManager, ct));

        foreach (var recipientUserId in recipientUserIds)
        {
            await notificationService.CreateAsync(recipientUserId, NotificationTypes.ExpenseAwaitingApproval,
                $"Expense #{expense.Id} ({expense.Amount:C}) is awaiting approval.", nameof(Expense), expense.Id, ct);
        }
    }

    public async Task ApproveAsync(int expenseId, string approverUserId, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);

        if (expense.Status != ExpenseStatus.Submitted)
            throw new ValidationFailedException("Only a submitted expense can be approved.");

        expense.Status = ExpenseStatus.Approved;
        expense.ApprovedByUserId = approverUserId;
        expense.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int expenseId, string approverUserId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationFailedException("A rejection reason is required.");

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);

        if (expense.Status != ExpenseStatus.Submitted)
            throw new ValidationFailedException("Only a submitted expense can be rejected.");

        expense.Status = ExpenseStatus.Rejected;
        expense.ApprovedByUserId = approverUserId;
        expense.ApprovedAtUtc = DateTime.UtcNow;
        expense.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReimburseAsync(int expenseId, string financeUserId, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);

        if (expense.Status != ExpenseStatus.Approved)
            throw new ValidationFailedException("Only an approved expense can be reimbursed.");

        expense.Status = ExpenseStatus.Reimbursed;
        expense.ReimbursedByUserId = financeUserId;
        expense.ReimbursedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Expense> LoadOwnedAsync(int expenseId, string userId, CancellationToken ct)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.CreatedByUserId == userId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);

        return expense;
    }
}

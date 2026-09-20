using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Expenses;

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> GetMineAsync(string userId, ExpenseStatus? status, PagedRequest request,
        CancellationToken ct = default);

    Task<int> CreateAsync(string userId, ExpenseSaveRequest request, CancellationToken ct = default);

    Task SubmitAsync(int expenseId, string userId, CancellationToken ct = default);

    Task ApproveAsync(int expenseId, string approverUserId, CancellationToken ct = default);

    Task RejectAsync(int expenseId, string approverUserId, string reason, CancellationToken ct = default);

    Task ReimburseAsync(int expenseId, string financeUserId, CancellationToken ct = default);
}

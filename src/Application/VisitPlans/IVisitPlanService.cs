using PharmaERP.Shared.Common;

namespace PharmaERP.Application.VisitPlans;

public interface IVisitPlanService
{
    Task<PagedResult<VisitPlanListItemDto>> GetListAsync(PagedRequest request, int? representativeId,
        Domain.Enums.VisitPlanStatus? status, CancellationToken ct = default);

    Task<VisitPlanDetailDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateDraftAsync(VisitPlanCreateRequest request, CancellationToken ct = default);

    Task<int> AddItemAsync(int visitPlanId, VisitPlanItemSaveRequest request, CancellationToken ct = default);

    Task RemoveItemAsync(int visitPlanId, int itemId, CancellationToken ct = default);

    Task SubmitAsync(int visitPlanId, CancellationToken ct = default);

    Task ApproveAsync(int visitPlanId, string approverUserId, CancellationToken ct = default);

    Task RejectAsync(int visitPlanId, string approverUserId, string reason, CancellationToken ct = default);

    /// <summary>Today's approved (or submitted, if approval isn't required) plan items for a representative — mobile "Today's Plan".</summary>
    Task<IReadOnlyList<VisitPlanItemDto>> GetTodaysPlanAsync(int representativeId, DateOnly date, CancellationToken ct = default);

    Task<PlanVsActualDto> GetPlanVsActualAsync(int visitPlanId, CancellationToken ct = default);
}

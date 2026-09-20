namespace PharmaERP.Application.Lists;

public interface IListService
{
    Task<IReadOnlyList<CustomerListSummaryDto>> GetListsAsync(CancellationToken ct = default);
    Task<CustomerListDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(CustomerListSaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, CustomerListSaveRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Adds one customer to a Static list. Throws for Dynamic lists (their membership is filter-computed).</summary>
    Task AddMemberAsync(int listId, int? doctorId, int? pharmacyId, CancellationToken ct = default);
    Task RemoveMemberAsync(int listId, int listItemId, CancellationToken ct = default);

    /// <summary>Reassigns every current member of the list to one representative in a single action
    /// (addendum 3.1: "100 Doctors + 50 Pharmacies to Rep Ahmed in one action"), logging a
    /// CustomerTransferLog row for each member that previously had a different representative.</summary>
    Task<BulkAssignResultDto> BulkAssignAsync(int listId, int toRepresentativeId, string? reason, CancellationToken ct = default);

    /// <summary>Reassigns a single Doctor or Pharmacy to a new representative, logging the handover.
    /// Past transactions keep whichever representative they were recorded under — only future activity follows the new owner.</summary>
    Task TransferCustomerAsync(CustomerTransferRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerTransferLogDto>> GetTransferLogAsync(int? doctorId, int? pharmacyId, CancellationToken ct = default);
}

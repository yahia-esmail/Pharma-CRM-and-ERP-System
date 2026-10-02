using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Collections;

public interface ICollectionService
{
    Task<PagedResult<CollectionDto>> GetCollectionsAsync(PagedRequest request, int? representativeId,
        int? pharmacyId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);

    Task<int> RecordCollectionAsync(int representativeId, CollectionSaveRequest request, CancellationToken ct = default);

    Task<RepFinancialCustodyDto> GetFinancialCustodyAsync(int representativeId, CancellationToken ct = default);

    /// <summary>Every representative's financial custody in scope — feeds the dashboard/outstanding alerts.</summary>
    Task<IReadOnlyList<RepFinancialCustodyDto>> GetFinancialCustodySummaryAsync(int? territoryId, CancellationToken ct = default);

    Task<PagedResult<RemittanceTransactionDto>> GetRemittancesAsync(PagedRequest request, int? representativeId,
        CancellationToken ct = default);

    /// <summary>Finance-role confirmation that funds were physically received (spec 4.6) — reduces outstanding balance.</summary>
    Task<int> RemitAsync(string receivingUserId, RemittanceSaveRequest request, CancellationToken ct = default);

    /// <summary>Attaches a photo/scan of proof of payment to a Collection (addendum 3.5).</summary>
    Task<int> AddAttachmentAsync(int collectionId, string uploadedByUserId, string fileName, string contentType,
        long sizeBytes, Stream content, CancellationToken ct = default,
        int? ownerRepresentativeId = null);

    Task<IReadOnlyList<CollectionAttachmentDto>> GetAttachmentsAsync(int collectionId, CancellationToken ct = default);

    Task<(Stream Content, string ContentType, string FileName)> OpenAttachmentAsync(int attachmentId, CancellationToken ct = default);

    /// <summary>Financial Reconciliation (addendum 3.6) — a cash count vs. the derived outstanding
    /// balance. A zero variance auto-approves (nothing to explain); a nonzero variance requires a Reason
    /// and stays Pending until ApproveReconciliationAsync — the outstanding balance never changes until then.</summary>
    Task<IReadOnlyList<FinancialReconciliationDto>> GetReconciliationsAsync(int? representativeId,
        FinancialReconciliationStatus? status, CancellationToken ct = default);

    Task<FinancialReconciliationResultDto> RequestReconciliationAsync(string requestedByUserId,
        FinancialReconciliationRequest request, CancellationToken ct = default);

    Task ApproveReconciliationAsync(int reconciliationId, string approvedByUserId, CancellationToken ct = default);

    Task RejectReconciliationAsync(int reconciliationId, string reason, CancellationToken ct = default);
}

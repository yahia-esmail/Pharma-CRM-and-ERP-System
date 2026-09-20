using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Application.Collections;

public class CollectionService(IAppDbContext db, IFileStorageService fileStorage, INotificationService notificationService,
    IUserDirectoryService userDirectory) : ICollectionService
{
    public async Task<PagedResult<CollectionDto>> GetCollectionsAsync(PagedRequest request, int? representativeId,
        int? pharmacyId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default)
    {
        var query = db.Collections.AsNoTracking().Where(c => !c.IsDeleted);

        if (representativeId.HasValue) query = query.Where(c => c.RepresentativeId == representativeId);
        if (pharmacyId.HasValue) query = query.Where(c => c.PharmacyId == pharmacyId);
        if (fromDate.HasValue) query = query.Where(c => c.CollectionDateUtc >= fromDate.Value.ToDateTime(TimeOnly.MinValue));
        if (toDate.HasValue) query = query.Where(c => c.CollectionDateUtc <= toDate.Value.ToDateTime(TimeOnly.MaxValue));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CollectionDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CollectionDto(c.Id, c.RepresentativeId, c.Representative.FullName, c.PharmacyId,
                c.Pharmacy.Name, c.SaleId, c.Amount, c.CollectionDateUtc, c.PaymentMethod, c.ReferenceNumber, c.Notes,
                db.CollectionAttachments.Count(a => a.CollectionId == c.Id && !a.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<CollectionDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<int> RecordCollectionAsync(int representativeId, CollectionSaveRequest request, CancellationToken ct = default)
    {
        var pharmacyExists = await db.Pharmacies.AnyAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct);
        if (!pharmacyExists) throw new NotFoundException(nameof(Pharmacy), request.PharmacyId);

        if (request.Amount <= 0)
            throw new ValidationFailedException("Amount must be greater than zero.");

        var collection = new Collection
        {
            RepresentativeId = representativeId,
            PharmacyId = request.PharmacyId,
            SaleId = request.SaleId,
            Amount = request.Amount,
            CollectionDateUtc = request.CollectionDateUtc ?? DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);

        // "Collection awaiting review" (addendum 3.10) — Collection has no formal review/approval status
        // today (unlike Orders/Returns/Reconciliation), so this simply lets Finance know a new collection
        // was recorded and is worth reviewing, rather than gating anything.
        foreach (var financeUserId in await userDirectory.GetUserIdsInRoleAsync(Roles.Finance, ct))
        {
            await notificationService.CreateAsync(financeUserId, NotificationTypes.CollectionAwaitingReview,
                $"New collection of {request.Amount:C} recorded — awaiting review.", nameof(Collection), collection.Id, ct);
        }

        return collection.Id;
    }

    public async Task<RepFinancialCustodyDto> GetFinancialCustodyAsync(int representativeId, CancellationToken ct = default)
    {
        var rep = await db.Representatives.AsNoTracking().FirstOrDefaultAsync(r => r.Id == representativeId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Representative), representativeId);

        return await BuildCustodyDtoAsync(rep, ct);
    }

    public async Task<IReadOnlyList<RepFinancialCustodyDto>> GetFinancialCustodySummaryAsync(int? territoryId,
        CancellationToken ct = default)
    {
        var repsQuery = db.Representatives.AsNoTracking().Where(r => !r.IsDeleted);
        if (territoryId.HasValue) repsQuery = repsQuery.Where(r => r.TerritoryId == territoryId);
        var reps = await repsQuery.OrderBy(r => r.FullName).ToListAsync(ct);

        var results = new List<RepFinancialCustodyDto>(reps.Count);
        foreach (var rep in reps)
            results.Add(await BuildCustodyDtoAsync(rep, ct));

        return results.OrderByDescending(r => r.OutstandingBalance).ToList();
    }

    public async Task<PagedResult<RemittanceTransactionDto>> GetRemittancesAsync(PagedRequest request,
        int? representativeId, CancellationToken ct = default)
    {
        var query = db.RemittanceTransactions.AsNoTracking().Where(r => !r.IsDeleted);
        if (representativeId.HasValue) query = query.Where(r => r.RepresentativeId == representativeId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.RemittanceDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RemittanceTransactionDto(r.Id, r.RepresentativeId, r.Representative.FullName, r.Amount,
                r.RemittanceDateUtc, r.RemittanceMethod, r.ReceivingUserId, r.ReferenceNumber))
            .ToListAsync(ct);

        return new PagedResult<RemittanceTransactionDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<int> RemitAsync(string receivingUserId, RemittanceSaveRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ValidationFailedException("Amount must be greater than zero.");

        var outstanding = await GetOutstandingBalanceAsync(request.RepresentativeId, ct);

        if (request.Amount > outstanding)
            throw new ValidationFailedException($"Remittance amount ({request.Amount:C}) exceeds the representative's outstanding balance ({outstanding:C}).");

        var remittance = new RemittanceTransaction
        {
            RepresentativeId = request.RepresentativeId,
            Amount = request.Amount,
            RemittanceDateUtc = DateTime.UtcNow,
            RemittanceMethod = request.RemittanceMethod,
            ReceivingUserId = receivingUserId,
            ReferenceNumber = request.ReferenceNumber
        };
        db.RemittanceTransactions.Add(remittance);
        await db.SaveChangesAsync(ct);
        return remittance.Id;
    }

    private async Task<RepFinancialCustodyDto> BuildCustodyDtoAsync(Representative rep, CancellationToken ct)
    {
        var collections = await db.Collections.AsNoTracking()
            .Where(c => c.RepresentativeId == rep.Id)
            .OrderBy(c => c.CollectionDateUtc)
            .Select(c => new { c.Amount, c.CollectionDateUtc })
            .ToListAsync(ct);

        var totalCollected = collections.Sum(c => c.Amount);
        var totalRemitted = await db.RemittanceTransactions.Where(r => r.RepresentativeId == rep.Id)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var approvedVariance = await GetApprovedReconciliationVarianceAsync(rep.Id, ct);

        // FIFO: walk collections oldest-first: everything covered by cumulative remittances is
        // "remitted", the first collection where the running total exceeds it is the oldest unremitted one.
        DateTime? oldestUnremitted = null;
        var running = 0m;
        foreach (var c in collections)
        {
            running += c.Amount;
            if (running > totalRemitted)
            {
                oldestUnremitted = c.CollectionDateUtc;
                break;
            }
        }

        return new RepFinancialCustodyDto(rep.Id, rep.FullName, totalCollected, totalRemitted,
            totalCollected - totalRemitted + approvedVariance, oldestUnremitted);
    }

    /// <summary>Shared "current outstanding balance" formula — Collected minus Remitted, adjusted by any
    /// Approved reconciliation variance (addendum 3.6). Every place that needs the balance (remittance
    /// validation, the custody summary, a new reconciliation's SystemBalance snapshot) calls this instead
    /// of re-deriving it, so there is exactly one definition of "outstanding."</summary>
    private async Task<decimal> GetOutstandingBalanceAsync(int representativeId, CancellationToken ct)
    {
        var totalCollected = await db.Collections.Where(c => c.RepresentativeId == representativeId)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var totalRemitted = await db.RemittanceTransactions.Where(r => r.RepresentativeId == representativeId)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var approvedVariance = await GetApprovedReconciliationVarianceAsync(representativeId, ct);
        return totalCollected - totalRemitted + approvedVariance;
    }

    private async Task<decimal> GetApprovedReconciliationVarianceAsync(int representativeId, CancellationToken ct)
    {
        var approved = await db.FinancialReconciliations.AsNoTracking()
            .Where(f => f.RepresentativeId == representativeId && f.Status == FinancialReconciliationStatus.Approved)
            .Select(f => new { f.SystemBalance, f.CountedBalance })
            .ToListAsync(ct);
        return approved.Sum(f => f.CountedBalance - f.SystemBalance);
    }

    public async Task<IReadOnlyList<FinancialReconciliationDto>> GetReconciliationsAsync(int? representativeId,
        FinancialReconciliationStatus? status, CancellationToken ct = default)
    {
        var query = db.FinancialReconciliations.AsNoTracking().Where(f => !f.IsDeleted);
        if (representativeId.HasValue) query = query.Where(f => f.RepresentativeId == representativeId);
        if (status.HasValue) query = query.Where(f => f.Status == status);

        return await query
            .OrderByDescending(f => f.ReconciliationDateUtc)
            .Select(f => new FinancialReconciliationDto(f.Id, f.RepresentativeId, f.Representative.FullName,
                f.ReconciliationDateUtc, f.SystemBalance, f.CountedBalance, f.CountedBalance - f.SystemBalance,
                f.Reason, f.Status, f.RequestedByUserId, f.ApprovedByUserId, f.ApprovedAtUtc, f.RejectionReason))
            .ToListAsync(ct);
    }

    public async Task<FinancialReconciliationResultDto> RequestReconciliationAsync(string requestedByUserId,
        FinancialReconciliationRequest request, CancellationToken ct = default)
    {
        var repExists = await db.Representatives.AnyAsync(r => r.Id == request.RepresentativeId && !r.IsDeleted, ct);
        if (!repExists) throw new NotFoundException(nameof(Representative), request.RepresentativeId);

        var systemBalance = await GetOutstandingBalanceAsync(request.RepresentativeId, ct);
        var variance = request.CountedBalance - systemBalance;

        if (variance != 0 && string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationFailedException("A reason is required to explain the difference before it can be submitted for approval.");

        var reconciliation = new FinancialReconciliation
        {
            RepresentativeId = request.RepresentativeId,
            ReconciliationDateUtc = DateTime.UtcNow,
            SystemBalance = systemBalance,
            CountedBalance = request.CountedBalance,
            Reason = request.Reason,
            RequestedByUserId = requestedByUserId,
            Status = variance == 0 ? FinancialReconciliationStatus.Approved : FinancialReconciliationStatus.Pending
        };
        if (variance == 0)
        {
            reconciliation.ApprovedByUserId = requestedByUserId;
            reconciliation.ApprovedAtUtc = DateTime.UtcNow;
        }

        db.FinancialReconciliations.Add(reconciliation);
        await db.SaveChangesAsync(ct);

        return new FinancialReconciliationResultDto(reconciliation.Id, systemBalance, request.CountedBalance, variance, variance != 0);
    }

    public async Task ApproveReconciliationAsync(int reconciliationId, string approvedByUserId, CancellationToken ct = default)
    {
        var reconciliation = await db.FinancialReconciliations.FirstOrDefaultAsync(f => f.Id == reconciliationId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FinancialReconciliation), reconciliationId);

        if (reconciliation.Status != FinancialReconciliationStatus.Pending)
            throw new ValidationFailedException("Only a pending reconciliation can be approved.");

        reconciliation.Status = FinancialReconciliationStatus.Approved;
        reconciliation.ApprovedByUserId = approvedByUserId;
        reconciliation.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectReconciliationAsync(int reconciliationId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationFailedException("A rejection reason is required.");

        var reconciliation = await db.FinancialReconciliations.FirstOrDefaultAsync(f => f.Id == reconciliationId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FinancialReconciliation), reconciliationId);

        if (reconciliation.Status != FinancialReconciliationStatus.Pending)
            throw new ValidationFailedException("Only a pending reconciliation can be rejected.");

        reconciliation.Status = FinancialReconciliationStatus.Rejected;
        reconciliation.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
    }

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "application/pdf" };
    private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024;

    public async Task<int> AddAttachmentAsync(int collectionId, string uploadedByUserId, string fileName,
        string contentType, long sizeBytes, Stream content, CancellationToken ct = default)
    {
        var collectionExists = await db.Collections.AnyAsync(c => c.Id == collectionId && !c.IsDeleted, ct);
        if (!collectionExists) throw new NotFoundException(nameof(Collection), collectionId);

        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationFailedException("Only JPEG, PNG, or PDF files can be attached as proof of payment.");
        if (sizeBytes > MaxAttachmentSizeBytes)
            throw new ValidationFailedException("Attachment must be 5 MB or smaller.");

        var relativePath = await fileStorage.SaveAsync($"collections/{collectionId}", fileName, content, ct);

        var attachment = new CollectionAttachment
        {
            CollectionId = collectionId,
            FileName = fileName,
            ContentType = contentType,
            RelativePath = relativePath,
            SizeBytes = sizeBytes,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByUserId = uploadedByUserId
        };
        db.CollectionAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return attachment.Id;
    }

    public async Task<IReadOnlyList<CollectionAttachmentDto>> GetAttachmentsAsync(int collectionId, CancellationToken ct = default)
    {
        return await db.CollectionAttachments.AsNoTracking()
            .Where(a => a.CollectionId == collectionId && !a.IsDeleted)
            .OrderBy(a => a.UploadedAtUtc)
            .Select(a => new CollectionAttachmentDto(a.Id, a.CollectionId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.CollectionAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CollectionAttachment), attachmentId);

        var stream = await fileStorage.OpenAsync(attachment.RelativePath, ct);
        return (stream, attachment.ContentType, attachment.FileName);
    }
}

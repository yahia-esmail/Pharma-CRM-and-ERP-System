using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Notifications;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Returns;

/// <summary>Formal Returns module (gap-analysis addendum 3.8) — both flows go through the same
/// request-then-approve workflow; the actual ledger effect only happens on approval, and is delegated to
/// ICustodyService so there's exactly one writer of CustodyTransaction/StockMovement rows in the system.</summary>
public class ReturnService(IAppDbContext db, ICustodyService custodyService, INotificationService notificationService) : IReturnService
{
    public async Task<IReadOnlyList<ReturnTransactionDto>> GetListAsync(int? representativeId, ReturnStatus? status,
        CancellationToken ct = default)
    {
        var query = db.ReturnTransactions.AsNoTracking().Where(r => !r.IsDeleted);
        if (representativeId.HasValue) query = query.Where(r => r.RepresentativeId == representativeId);
        if (status.HasValue) query = query.Where(r => r.Status == status);

        return await query
            .OrderByDescending(r => r.RequestedAtUtc)
            .Select(r => new ReturnTransactionDto(r.Id, r.FlowType, r.RepresentativeId, r.Representative.FullName,
                r.PharmacyId, r.Pharmacy != null ? r.Pharmacy.Name : null,
                r.WarehouseId, r.Warehouse != null ? r.Warehouse.Name : null,
                r.ProductId, r.Product.Name, r.ProductBatchId, r.ProductBatch != null ? r.ProductBatch.BatchNumber : null,
                r.Quantity, r.Reason, r.Notes, r.Status, r.RequestedByUserId, r.RequestedAtUtc,
                r.ApprovedByUserId, r.ApprovedAtUtc, r.RejectionReason))
            .ToListAsync(ct);
    }

    public async Task<int> RequestAsync(int representativeId, string requestedByUserId, ReturnRequestSaveRequest request,
        CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var productExists = await db.Products.AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct);
        if (!productExists) throw new NotFoundException(nameof(Product), request.ProductId);
        if (request.ProductBatchId is { } batchId
            && !await db.ProductBatches.AnyAsync(b => b.Id == batchId && b.ProductId == request.ProductId && !b.IsDeleted, ct))
            throw new ValidationFailedException("That batch isn't a batch of the selected product.");

        if (request.FlowType == ReturnFlowType.CustomerToRepresentative)
        {
            if (request.PharmacyId is null)
                throw new ValidationFailedException("Select which pharmacy the stock is coming back from.");
            var pharmacyExists = await db.Pharmacies.AnyAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct);
            if (!pharmacyExists) throw new NotFoundException(nameof(Pharmacy), request.PharmacyId.Value);
        }
        else
        {
            if (request.WarehouseId is null)
                throw new ValidationFailedException("Select which warehouse the stock is going back to.");
            var warehouseExists = await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId && !w.IsDeleted, ct);
            if (!warehouseExists) throw new NotFoundException(nameof(Warehouse), request.WarehouseId.Value);

            // Fail fast at request time — ApproveAsync re-checks anyway (the balance can move between
            // request and approval), but there's no point queuing a request that's already impossible.
            var balance = await custodyService.GetBalanceAsync(representativeId, request.ProductId, request.ProductBatchId, ct);
            if (balance < request.Quantity)
                throw new ValidationFailedException($"Insufficient custody balance to return (available: {balance}).");
        }

        var returnTransaction = new ReturnTransaction
        {
            FlowType = request.FlowType,
            RepresentativeId = representativeId,
            PharmacyId = request.FlowType == ReturnFlowType.CustomerToRepresentative ? request.PharmacyId : null,
            WarehouseId = request.FlowType == ReturnFlowType.RepresentativeToWarehouse ? request.WarehouseId : null,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = ReturnStatus.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = DateTime.UtcNow
        };
        db.ReturnTransactions.Add(returnTransaction);
        await db.SaveChangesAsync(ct);
        return returnTransaction.Id;
    }

    public async Task ApproveAsync(int returnId, string approvedByUserId, CancellationToken ct = default)
    {
        var returnTransaction = await db.ReturnTransactions.FirstOrDefaultAsync(r => r.Id == returnId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ReturnTransaction), returnId);

        if (returnTransaction.Status != ReturnStatus.Pending)
            throw new ValidationFailedException("Only a pending return can be approved.");

        if (returnTransaction.FlowType == ReturnFlowType.CustomerToRepresentative)
        {
            await custodyService.ReceiveCustomerReturnAsync(returnTransaction.RepresentativeId, returnTransaction.ProductId,
                returnTransaction.ProductBatchId, returnTransaction.Quantity,
                $"Customer return ({returnTransaction.Reason})", ct);
        }
        else
        {
            // Re-validated here (not just at request time) since the rep's balance can move in between.
            await custodyService.ReturnAsync(new ReturnFromCustodyRequest
            {
                RepresentativeId = returnTransaction.RepresentativeId,
                WarehouseId = returnTransaction.WarehouseId!.Value,
                ProductId = returnTransaction.ProductId,
                ProductBatchId = returnTransaction.ProductBatchId,
                Quantity = returnTransaction.Quantity,
                ReasonCode = $"Return approved ({returnTransaction.Reason})"
            }, ct);
        }

        returnTransaction.Status = ReturnStatus.Approved;
        returnTransaction.ApprovedByUserId = approvedByUserId;
        returnTransaction.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await notificationService.CreateAsync(returnTransaction.RequestedByUserId, NotificationTypes.ReturnApproved,
            $"Your return #{returnTransaction.Id} ({returnTransaction.Quantity} unit(s)) was approved.",
            nameof(ReturnTransaction), returnTransaction.Id, ct);
    }

    public async Task RejectAsync(int returnId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationFailedException("A rejection reason is required.");

        var returnTransaction = await db.ReturnTransactions.FirstOrDefaultAsync(r => r.Id == returnId && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ReturnTransaction), returnId);

        if (returnTransaction.Status != ReturnStatus.Pending)
            throw new ValidationFailedException("Only a pending return can be rejected.");

        returnTransaction.Status = ReturnStatus.Rejected;
        returnTransaction.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
        await notificationService.CreateAsync(returnTransaction.RequestedByUserId, NotificationTypes.ReturnRejected,
            $"Your return #{returnTransaction.Id} was rejected: {reason}", nameof(ReturnTransaction), returnTransaction.Id, ct);
    }
}

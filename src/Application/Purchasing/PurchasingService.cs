using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Warehouses;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Purchasing;

public class PurchasingService(IAppDbContext db, IWarehouseService warehouseService) : IPurchasingService
{
    public async Task<PagedResult<PurchaseOrderListItemDto>> GetListAsync(PagedRequest request, int? supplierId,
        PurchaseOrderStatus? status, CancellationToken ct = default)
    {
        var query = db.PurchaseOrders.AsNoTracking().Where(o => !o.IsDeleted);
        if (supplierId.HasValue) query = query.Where(o => o.SupplierId == supplierId);
        if (status.HasValue) query = query.Where(o => o.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.OrderDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new PurchaseOrderListItemDto(o.Id, o.SupplierId, o.Supplier.Name, o.OrderDateUtc,
                o.ExpectedDeliveryDate, o.Status, o.Lines.Sum(l => (decimal?)l.Quantity * l.UnitCost) ?? 0m))
            .ToListAsync(ct);

        return new PagedResult<PurchaseOrderListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<PurchaseOrderDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Lines.Where(l => !l.IsDeleted))
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);

        var receivedByLine = await db.PurchaseReceiptLines.AsNoTracking()
            .Where(l => order.Lines.Select(x => x.Id).Contains(l.PurchaseOrderLineId))
            .GroupBy(l => l.PurchaseOrderLineId)
            .Select(g => new { PurchaseOrderLineId = g.Key, Quantity = g.Sum(x => x.QuantityReceived) })
            .ToDictionaryAsync(x => x.PurchaseOrderLineId, x => x.Quantity, ct);

        var lines = order.Lines.Select(l => new PurchaseOrderLineDto(l.Id, l.ProductId, l.Product.Name, l.Quantity,
            l.UnitCost, l.Quantity * l.UnitCost, receivedByLine.GetValueOrDefault(l.Id))).ToList();

        return new PurchaseOrderDetailDto(order.Id, order.SupplierId, order.Supplier.Name, order.OrderDateUtc,
            order.ExpectedDeliveryDate, order.Status, lines, lines.Sum(l => l.LineTotal));
    }

    public async Task<int> CreateDraftAsync(PurchaseOrderCreateRequest request, CancellationToken ct = default)
    {
        var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct);
        if (!supplierExists) throw new NotFoundException(nameof(Supplier), request.SupplierId);

        var order = new PurchaseOrder
        {
            SupplierId = request.SupplierId,
            OrderDateUtc = DateTime.UtcNow,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Status = PurchaseOrderStatus.Draft
        };
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task<int> AddLineAsync(int purchaseOrderId, PurchaseOrderLineSaveRequest request, CancellationToken ct = default)
    {
        var order = await LoadDraftOrderAsync(purchaseOrderId, ct);

        var productExists = await db.Products.AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct);
        if (!productExists) throw new NotFoundException(nameof(Product), request.ProductId);
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var line = new PurchaseOrderLine
        {
            PurchaseOrderId = order.Id,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost
        };
        db.PurchaseOrderLines.Add(line);
        await db.SaveChangesAsync(ct);
        return line.Id;
    }

    public async Task RemoveLineAsync(int purchaseOrderId, int lineId, CancellationToken ct = default)
    {
        await LoadDraftOrderAsync(purchaseOrderId, ct);

        var line = await db.PurchaseOrderLines.FirstOrDefaultAsync(
            l => l.Id == lineId && l.PurchaseOrderId == purchaseOrderId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrderLine), lineId);

        line.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task SendAsync(int purchaseOrderId, CancellationToken ct = default)
    {
        var order = await LoadDraftOrderAsync(purchaseOrderId, ct);

        var hasLines = await db.PurchaseOrderLines.AnyAsync(l => l.PurchaseOrderId == purchaseOrderId && !l.IsDeleted, ct);
        if (!hasLines)
            throw new ValidationFailedException("Add at least one line before sending the purchase order.");

        order.Status = PurchaseOrderStatus.Sent;
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int purchaseOrderId, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == purchaseOrderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent))
            throw new ValidationFailedException("Only a draft or sent purchase order (with nothing received yet) can be cancelled.");

        order.Status = PurchaseOrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> ReceiveAsync(PurchaseReceiptRequest request, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == request.PurchaseOrderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId);

        if (order.Status is not (PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived))
            throw new ValidationFailedException("Only a sent or partially-received purchase order can receive goods.");
        if (request.Lines.Count == 0)
            throw new ValidationFailedException("Add at least one receipt line.");

        var receipt = new PurchaseReceipt
        {
            PurchaseOrderId = order.Id,
            WarehouseId = request.WarehouseId,
            ReceiptDateUtc = DateTime.UtcNow
        };
        db.PurchaseReceipts.Add(receipt);
        await db.SaveChangesAsync(ct); // need the receipt's id for its lines below

        foreach (var lineRequest in request.Lines)
        {
            var poLine = order.Lines.FirstOrDefault(l => l.Id == lineRequest.PurchaseOrderLineId)
                ?? throw new NotFoundException(nameof(PurchaseOrderLine), lineRequest.PurchaseOrderLineId);

            if (lineRequest.Quantity <= 0)
                throw new ValidationFailedException("Received quantity must be greater than zero.");

            var alreadyReceived = await db.PurchaseReceiptLines
                .Where(l => l.PurchaseOrderLineId == poLine.Id)
                .SumAsync(l => (int?)l.QuantityReceived, ct) ?? 0;

            if (alreadyReceived + lineRequest.Quantity > poLine.Quantity)
                throw new ValidationFailedException(
                    $"Receiving {lineRequest.Quantity} would exceed the ordered quantity for this line " +
                    $"(ordered: {poLine.Quantity}, already received: {alreadyReceived}).");

            var movementId = await warehouseService.ReceiveGoodsAsync(new GoodsReceiptRequest
            {
                WarehouseId = request.WarehouseId,
                ProductId = poLine.ProductId,
                BatchNumber = lineRequest.BatchNumber,
                ManufactureDate = lineRequest.ManufactureDate,
                ExpiryDate = lineRequest.ExpiryDate,
                Quantity = lineRequest.Quantity,
                ReferenceNote = $"PO #{order.Id} receipt"
            }, ct);

            var movement = await db.StockMovements.AsNoTracking().FirstAsync(m => m.Id == movementId, ct);

            db.PurchaseReceiptLines.Add(new PurchaseReceiptLine
            {
                PurchaseReceiptId = receipt.Id,
                PurchaseOrderLineId = poLine.Id,
                ProductId = poLine.ProductId,
                ProductBatchId = movement.ProductBatchId,
                QuantityReceived = lineRequest.Quantity,
                StockMovementId = movementId
            });
        }

        await db.SaveChangesAsync(ct);

        // Roll the PO status forward based on total received vs. ordered across all lines.
        var totalOrdered = order.Lines.Sum(l => l.Quantity);
        var totalReceived = await db.PurchaseReceiptLines
            .Where(l => order.Lines.Select(x => x.Id).Contains(l.PurchaseOrderLineId))
            .SumAsync(l => (int?)l.QuantityReceived, ct) ?? 0;

        order.Status = totalReceived >= totalOrdered ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        await db.SaveChangesAsync(ct);

        return receipt.Id;
    }

    private async Task<PurchaseOrder> LoadDraftOrderAsync(int purchaseOrderId, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == purchaseOrderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        if (order.Status != PurchaseOrderStatus.Draft)
            throw new ValidationFailedException("Only a draft purchase order can be edited.");

        return order;
    }
}

using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Traceability;

public class TraceService(IAppDbContext db) : ITraceService
{
    public async Task<BatchTraceDto> TraceBatchAsync(int productBatchId, CancellationToken ct = default)
    {
        var batch = await db.ProductBatches.AsNoTracking()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == productBatchId && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ProductBatch), productBatchId);

        return await BuildBatchTraceAsync(batch, ct);
    }

    public async Task<SaleTraceDto> TraceSaleAsync(int saleId, CancellationToken ct = default)
    {
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Pharmacy)
            .Include(s => s.Representative)
            .FirstOrDefaultAsync(s => s.Id == saleId && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Sale), saleId);

        var collections = await db.Collections.AsNoTracking()
            .Where(c => c.SaleId == saleId && !c.IsDeleted)
            .Select(c => new TraceCollectionDto(c.Id, c.Amount, c.CollectionDateUtc, c.PaymentMethod))
            .ToListAsync(ct);

        var totalCollected = await db.Collections.AsNoTracking()
            .Where(c => c.RepresentativeId == sale.RepresentativeId && !c.IsDeleted)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var totalRemitted = await db.RemittanceTransactions.AsNoTracking()
            .Where(r => r.RepresentativeId == sale.RepresentativeId && !r.IsDeleted)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

        var soldEntries = await db.CustodyTransactions.AsNoTracking()
            .Where(c => c.SaleId == saleId && !c.IsDeleted)
            .Select(c => c.ProductBatchId)
            .ToListAsync(ct);

        var batches = new List<BatchTraceDto>();
        foreach (var batchId in soldEntries.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            var batch = await db.ProductBatches.AsNoTracking().Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is not null)
                batches.Add(await BuildBatchTraceAsync(batch, ct));
        }

        return new SaleTraceDto(sale.Id, sale.OrderId, sale.PharmacyId, sale.Pharmacy.Name, sale.RepresentativeId,
            sale.Representative.FullName, sale.TotalAmount, sale.SaleDateUtc, collections,
            totalCollected - totalRemitted, batches);
    }

    private async Task<BatchTraceDto> BuildBatchTraceAsync(ProductBatch batch, CancellationToken ct)
    {
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductBatchId == batch.Id && !m.IsDeleted)
            .Include(m => m.SourceWarehouse)
            .Include(m => m.DestinationWarehouse)
            .Include(m => m.Representative)
            .OrderBy(m => m.MovementDateUtc)
            .Select(m => new TraceMovementDto(m.Id, m.MovementType, m.Quantity,
                m.SourceWarehouse != null ? m.SourceWarehouse.Name : m.MovementType == Domain.Enums.StockMovementType.ReturnFromCustody ? m.Representative!.FullName : null,
                m.DestinationWarehouse != null ? m.DestinationWarehouse.Name : m.MovementType == Domain.Enums.StockMovementType.IssueToCustody ? m.Representative!.FullName : null,
                m.MovementDateUtc, m.ReasonCode, m.ReferenceNote))
            .ToListAsync(ct);

        var goodsReceiptMovementIds = movements
            .Where(m => m.MovementType == Domain.Enums.StockMovementType.GoodsReceipt)
            .Select(m => m.Id).ToList();

        var origins = await db.PurchaseReceiptLines.AsNoTracking()
            .Where(l => goodsReceiptMovementIds.Contains(l.StockMovementId) && !l.IsDeleted)
            .Include(l => l.PurchaseReceipt).ThenInclude(r => r.PurchaseOrder).ThenInclude(o => o.Supplier)
            .Include(l => l.PurchaseOrderLine)
            .Select(l => new TraceOriginDto(l.PurchaseReceipt.PurchaseOrder.SupplierId,
                l.PurchaseReceipt.PurchaseOrder.Supplier.Name, l.PurchaseReceipt.PurchaseOrderId,
                l.PurchaseReceiptId, l.PurchaseReceipt.ReceiptDateUtc, l.PurchaseOrderLine.UnitCost))
            .ToListAsync(ct);

        var custodyEntries = await db.CustodyTransactions.AsNoTracking()
            .Where(c => c.ProductBatchId == batch.Id && !c.IsDeleted)
            .Include(c => c.Representative)
            .OrderBy(c => c.TransactionDateUtc)
            .Select(c => new TraceCustodyEntryDto(c.Id, c.RepresentativeId, c.Representative.FullName,
                c.TransactionType, c.Quantity, c.TransactionDateUtc, c.SaleId, c.ReasonCode))
            .ToListAsync(ct);

        var saleIds = custodyEntries.Where(c => c.SaleId.HasValue).Select(c => c.SaleId!.Value).Distinct().ToList();
        var sales = new List<TraceSaleSummaryDto>();
        foreach (var saleId in saleIds)
        {
            var sale = await db.Sales.AsNoTracking().Include(s => s.Pharmacy).Include(s => s.Representative)
                .FirstOrDefaultAsync(s => s.Id == saleId, ct);
            if (sale is null) continue;

            var collections = await db.Collections.AsNoTracking()
                .Where(c => c.SaleId == saleId && !c.IsDeleted)
                .Select(c => new TraceCollectionDto(c.Id, c.Amount, c.CollectionDateUtc, c.PaymentMethod))
                .ToListAsync(ct);

            sales.Add(new TraceSaleSummaryDto(sale.Id, sale.OrderId, sale.PharmacyId, sale.Pharmacy.Name,
                sale.RepresentativeId, sale.Representative.FullName, sale.TotalAmount, sale.SaleDateUtc, collections));
        }

        return new BatchTraceDto(batch.Id, batch.ProductId, batch.Product.Name, batch.BatchNumber, batch.ExpiryDate,
            origins, movements, custodyEntries, sales);
    }
}

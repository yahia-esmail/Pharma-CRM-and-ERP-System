using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Warehouses;

public class WarehouseService(IAppDbContext db) : IWarehouseService
{
    public async Task<IReadOnlyList<WarehouseListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        return await db.Warehouses.AsNoTracking()
            .Where(w => !w.IsDeleted)
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseListItemDto(w.Id, w.Name, w.Location, w.Type))
            .ToListAsync(ct);
    }

    public async Task<int> CreateAsync(WarehouseSaveRequest request, CancellationToken ct = default)
    {
        var warehouse = new Warehouse();
        Apply(warehouse, request);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return warehouse.Id;
    }

    public async Task UpdateAsync(int id, WarehouseSaveRequest request, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        Apply(warehouse, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        var hasMovements = await db.StockMovements.AnyAsync(
            m => m.SourceWarehouseId == id || m.DestinationWarehouseId == id, ct);
        if (hasMovements)
            throw new ValidationFailedException("Cannot delete a warehouse that already has stock movement history.");

        warehouse.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProductBatchDto>> GetBatchesAsync(int? productId, CancellationToken ct = default)
    {
        var query = db.ProductBatches.AsNoTracking().Where(b => !b.IsDeleted);
        if (productId.HasValue) query = query.Where(b => b.ProductId == productId);

        return await query
            .OrderBy(b => b.ExpiryDate)
            .Select(b => new ProductBatchDto(b.Id, b.ProductId, b.Product.Name, b.BatchNumber, b.ManufactureDate, b.ExpiryDate))
            .ToListAsync(ct);
    }

    public async Task<int> CreateBatchAsync(ProductBatchSaveRequest request, CancellationToken ct = default)
    {
        var duplicate = await db.ProductBatches.AnyAsync(
            b => b.ProductId == request.ProductId && b.BatchNumber == request.BatchNumber && !b.IsDeleted, ct);
        if (duplicate)
            throw new ValidationFailedException($"Batch '{request.BatchNumber}' already exists for this product.");

        var batch = new ProductBatch
        {
            ProductId = request.ProductId,
            BatchNumber = request.BatchNumber,
            ManufactureDate = request.ManufactureDate,
            ExpiryDate = request.ExpiryDate
        };
        db.ProductBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return batch.Id;
    }

    public async Task<IReadOnlyList<StockLevelDto>> GetStockLevelsAsync(int? warehouseId, int? productId,
        CancellationToken ct = default)
    {
        var balances = await ComputeWarehouseBalancesAsync(warehouseId, productId, ct);

        var warehouses = await db.Warehouses.AsNoTracking().Where(w => !w.IsDeleted)
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var products = await db.Products.AsNoTracking().Where(p => !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var batches = await db.ProductBatches.AsNoTracking().Where(b => !b.IsDeleted)
            .ToDictionaryAsync(b => b.Id, b => (b.BatchNumber, b.ExpiryDate), ct);

        return balances.Where(b => b.Value != 0)
            .Select(b => new StockLevelDto(
                b.Key.WarehouseId, warehouses.GetValueOrDefault(b.Key.WarehouseId, "?"),
                b.Key.ProductId, products.GetValueOrDefault(b.Key.ProductId, "?"),
                b.Key.ProductBatchId,
                b.Key.ProductBatchId.HasValue ? batches.GetValueOrDefault(b.Key.ProductBatchId.Value).BatchNumber : null,
                b.Key.ProductBatchId.HasValue ? batches.GetValueOrDefault(b.Key.ProductBatchId.Value).ExpiryDate : null,
                b.Value))
            .OrderBy(s => s.ProductName).ThenBy(s => s.WarehouseName)
            .ToList();
    }

    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(PagedRequest request, int? warehouseId,
        int? productId, CancellationToken ct = default)
    {
        var query = db.StockMovements.AsNoTracking().Where(m => !m.IsDeleted);

        if (warehouseId.HasValue)
            query = query.Where(m => m.SourceWarehouseId == warehouseId || m.DestinationWarehouseId == warehouseId);
        if (productId.HasValue)
            query = query.Where(m => m.ProductId == productId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.MovementDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new StockMovementDto(m.Id, m.MovementType, m.ProductId, m.Product.Name,
                m.ProductBatch != null ? m.ProductBatch.BatchNumber : null, m.Quantity,
                m.SourceWarehouse != null ? m.SourceWarehouse.Name : null,
                m.DestinationWarehouse != null ? m.DestinationWarehouse.Name : null,
                m.Representative != null ? m.Representative.FullName : null,
                m.MovementDateUtc, m.ReasonCode, m.ReferenceNote))
            .ToListAsync(ct);

        return new PagedResult<StockMovementDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<int> ReceiveGoodsAsync(GoodsReceiptRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        int? batchId = null;
        if (product.IsBatchTracked)
        {
            if (string.IsNullOrWhiteSpace(request.BatchNumber) || request.ExpiryDate is null)
                throw new ValidationFailedException("Batch number and expiry date are required for a batch-tracked product.");

            var batch = await db.ProductBatches.FirstOrDefaultAsync(
                b => b.ProductId == request.ProductId && b.BatchNumber == request.BatchNumber && !b.IsDeleted, ct);
            if (batch is null)
            {
                batch = new ProductBatch
                {
                    ProductId = request.ProductId,
                    BatchNumber = request.BatchNumber,
                    ManufactureDate = request.ManufactureDate,
                    ExpiryDate = request.ExpiryDate.Value
                };
                db.ProductBatches.Add(batch);
                await db.SaveChangesAsync(ct); // need the generated id before referencing it below
            }
            batchId = batch.Id;
        }

        var movement = new StockMovement
        {
            MovementType = StockMovementType.GoodsReceipt,
            ProductId = request.ProductId,
            ProductBatchId = batchId,
            Quantity = request.Quantity,
            DestinationWarehouseId = request.WarehouseId,
            MovementDateUtc = DateTime.UtcNow,
            ReferenceNote = request.ReferenceNote
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement.Id;
    }

    public async Task<int> TransferAsync(StockTransferRequest request, CancellationToken ct = default)
    {
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
            throw new ValidationFailedException("Source and destination warehouses must be different.");
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var available = await GetSingleBalanceAsync(request.SourceWarehouseId, request.ProductId, request.ProductBatchId, ct);
        if (available < request.Quantity)
            throw new ValidationFailedException($"Insufficient stock at the source warehouse (available: {available}).");

        var movement = new StockMovement
        {
            MovementType = StockMovementType.Transfer,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity,
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            MovementDateUtc = DateTime.UtcNow,
            ReferenceNote = request.ReferenceNote
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement.Id;
    }

    public async Task<int> AdjustAsync(StockAdjustmentRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        if (!request.IsIncrease)
        {
            var available = await GetSingleBalanceAsync(request.WarehouseId, request.ProductId, request.ProductBatchId, ct);
            if (available < request.Quantity)
                throw new ValidationFailedException($"Insufficient stock to decrease (available: {available}).");
        }

        var movement = new StockMovement
        {
            MovementType = request.IsIncrease ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity,
            SourceWarehouseId = request.IsIncrease ? null : request.WarehouseId,
            DestinationWarehouseId = request.IsIncrease ? request.WarehouseId : null,
            MovementDateUtc = DateTime.UtcNow,
            ReasonCode = request.ReasonCode
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement.Id;
    }

    public async Task<IReadOnlyList<NearExpiryBatchDto>> GetNearExpiryAsync(int thresholdDays, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(thresholdDays));
        var batches = await db.ProductBatches.AsNoTracking()
            .Where(b => !b.IsDeleted && b.ExpiryDate <= cutoff)
            .Include(b => b.Product)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);

        var results = new List<NearExpiryBatchDto>();
        foreach (var batch in batches)
        {
            var warehouseQty = await db.StockMovements.Where(m => m.ProductBatchId == batch.Id)
                .SumAsync(m => (m.DestinationWarehouseId != null ? m.Quantity : 0) - (m.SourceWarehouseId != null ? m.Quantity : 0), ct);
            var custodyQty = await db.CustodyTransactions.Where(c => c.ProductBatchId == batch.Id).SumAsync(c => (int?)c.Quantity, ct) ?? 0;

            if (warehouseQty == 0 && custodyQty == 0) continue;

            results.Add(new NearExpiryBatchDto(batch.Id, batch.ProductId, batch.Product.Name, batch.BatchNumber,
                batch.ExpiryDate, batch.ExpiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber,
                warehouseQty, custodyQty));
        }

        return results;
    }

    public async Task<IReadOnlyList<LowStockProductDto>> GetLowStockAsync(CancellationToken ct = default)
    {
        var products = await db.Products.AsNoTracking().Where(p => !p.IsDeleted && p.ReorderLevel > 0).ToListAsync(ct);

        var results = new List<LowStockProductDto>();
        foreach (var product in products)
        {
            var totalQty = await db.StockMovements.Where(m => m.ProductId == product.Id)
                .SumAsync(m => (m.DestinationWarehouseId != null ? m.Quantity : 0) - (m.SourceWarehouseId != null ? m.Quantity : 0), ct);

            if (totalQty <= product.ReorderLevel)
                results.Add(new LowStockProductDto(product.Id, product.Name, product.Sku, product.ReorderLevel, totalQty));
        }

        return results.OrderBy(r => r.TotalWarehouseQuantity).ToList();
    }

    /// <summary>Net balance for one warehouse/product/(batch) combination — inbound minus outbound movements.</summary>
    private async Task<int> GetSingleBalanceAsync(int warehouseId, int productId, int? batchId, CancellationToken ct)
    {
        var inbound = await db.StockMovements
            .Where(m => m.DestinationWarehouseId == warehouseId && m.ProductId == productId && m.ProductBatchId == batchId)
            .SumAsync(m => (int?)m.Quantity, ct) ?? 0;
        var outbound = await db.StockMovements
            .Where(m => m.SourceWarehouseId == warehouseId && m.ProductId == productId && m.ProductBatchId == batchId)
            .SumAsync(m => (int?)m.Quantity, ct) ?? 0;
        return inbound - outbound;
    }

    private async Task<Dictionary<(int WarehouseId, int ProductId, int? ProductBatchId), int>> ComputeWarehouseBalancesAsync(
        int? warehouseId, int? productId, CancellationToken ct)
    {
        var inboundQuery = db.StockMovements.AsNoTracking().Where(m => m.DestinationWarehouseId != null);
        var outboundQuery = db.StockMovements.AsNoTracking().Where(m => m.SourceWarehouseId != null);
        if (warehouseId.HasValue)
        {
            inboundQuery = inboundQuery.Where(m => m.DestinationWarehouseId == warehouseId);
            outboundQuery = outboundQuery.Where(m => m.SourceWarehouseId == warehouseId);
        }
        if (productId.HasValue)
        {
            inboundQuery = inboundQuery.Where(m => m.ProductId == productId);
            outboundQuery = outboundQuery.Where(m => m.ProductId == productId);
        }

        var inbound = await inboundQuery
            .GroupBy(m => new { WarehouseId = m.DestinationWarehouseId!.Value, m.ProductId, m.ProductBatchId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ProductId, g.Key.ProductBatchId, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        var outbound = await outboundQuery
            .GroupBy(m => new { WarehouseId = m.SourceWarehouseId!.Value, m.ProductId, m.ProductBatchId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ProductId, g.Key.ProductBatchId, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        var balances = new Dictionary<(int, int, int?), int>();
        foreach (var row in inbound)
        {
            var key = (row.WarehouseId, row.ProductId, row.ProductBatchId);
            balances[key] = balances.GetValueOrDefault(key) + row.Quantity;
        }
        foreach (var row in outbound)
        {
            var key = (row.WarehouseId, row.ProductId, row.ProductBatchId);
            balances[key] = balances.GetValueOrDefault(key) - row.Quantity;
        }

        return balances;
    }

    private static void Apply(Warehouse warehouse, WarehouseSaveRequest request)
    {
        warehouse.Name = request.Name;
        warehouse.Location = request.Location;
        warehouse.Type = request.Type;
        warehouse.ResponsibleUserId = request.ResponsibleUserId;
    }
}

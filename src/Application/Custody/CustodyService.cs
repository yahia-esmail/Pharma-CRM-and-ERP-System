using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Custody;

public class CustodyService(IAppDbContext db) : ICustodyService
{
    public async Task<IReadOnlyList<RepStockCustodyBalanceDto>> GetBalancesAsync(int representativeId,
        CancellationToken ct = default)
    {
        var rows = await db.CustodyTransactions.AsNoTracking()
            .Where(c => c.RepresentativeId == representativeId)
            .GroupBy(c => new { c.ProductId, c.ProductBatchId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductBatchId,
                Received = g.Where(x => x.Quantity > 0).Sum(x => x.Quantity),
                Sold = -g.Where(x => x.TransactionType == CustodyTransactionType.Sold).Sum(x => x.Quantity),
                Returned = -g.Where(x => x.TransactionType == CustodyTransactionType.Returned).Sum(x => x.Quantity),
                Balance = g.Sum(x => x.Quantity)
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var products = await db.Products.AsNoTracking()
            .Where(p => rows.Select(r => r.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var batchIds = rows.Where(r => r.ProductBatchId.HasValue).Select(r => r.ProductBatchId!.Value).Distinct().ToList();
        var batches = await db.ProductBatches.AsNoTracking()
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => (b.BatchNumber, b.ExpiryDate), ct);

        return rows.Select(r => new RepStockCustodyBalanceDto(
                representativeId, r.ProductId, products.GetValueOrDefault(r.ProductId, "?"),
                r.ProductBatchId,
                r.ProductBatchId.HasValue ? batches.GetValueOrDefault(r.ProductBatchId.Value).BatchNumber : null,
                r.ProductBatchId.HasValue ? batches.GetValueOrDefault(r.ProductBatchId.Value).ExpiryDate : null,
                r.Received, r.Sold, r.Returned, r.Balance))
            .OrderBy(r => r.ProductName)
            .ToList();
    }

    public async Task<IReadOnlyList<CustodyTransactionDto>> GetLedgerAsync(int representativeId, int? productId,
        CancellationToken ct = default)
    {
        var query = db.CustodyTransactions.AsNoTracking().Where(c => c.RepresentativeId == representativeId);
        if (productId.HasValue) query = query.Where(c => c.ProductId == productId);

        return await query
            .OrderByDescending(c => c.TransactionDateUtc)
            .Select(c => new CustodyTransactionDto(c.Id, c.TransactionType, c.ProductId, c.Product.Name,
                c.ProductBatch != null ? c.ProductBatch.BatchNumber : null, c.Quantity, c.TransactionDateUtc,
                c.ReasonCode, c.SaleId, c.SourceStockMovementId))
            .ToListAsync(ct);
    }

    public async Task<int> IssueAsync(IssueToCustodyRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var batchId = request.ProductBatchId;
        if (product.IsBatchTracked && batchId is null)
        {
            // FEFO default (spec 4.4): pick the warehouse's earliest-expiring batch with enough stock.
            batchId = await PickFefoWarehouseBatchAsync(request.WarehouseId, request.ProductId, request.Quantity, ct);
        }

        var warehouseBalance = await GetWarehouseBalanceAsync(request.WarehouseId, request.ProductId, batchId, ct);
        if (warehouseBalance < request.Quantity)
            throw new ValidationFailedException($"Insufficient stock at the warehouse (available: {warehouseBalance}).");

        var now = DateTime.UtcNow;
        var movement = new StockMovement
        {
            MovementType = StockMovementType.IssueToCustody,
            ProductId = request.ProductId,
            ProductBatchId = batchId,
            Quantity = request.Quantity,
            SourceWarehouseId = request.WarehouseId,
            RepresentativeId = request.RepresentativeId,
            MovementDateUtc = now,
            ReferenceNote = request.ReferenceNote
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct); // need the movement's id for the linked custody transaction below

        db.CustodyTransactions.Add(new CustodyTransaction
        {
            RepresentativeId = request.RepresentativeId,
            ProductId = request.ProductId,
            ProductBatchId = batchId,
            TransactionType = CustodyTransactionType.Received,
            Quantity = request.Quantity,
            SourceStockMovementId = movement.Id,
            TransactionDateUtc = now
        });
        await db.SaveChangesAsync(ct);

        return movement.Id;
    }

    public async Task<int> ReturnAsync(ReturnFromCustodyRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var custodyBalance = await GetCustodyBalanceAsync(request.RepresentativeId, request.ProductId, request.ProductBatchId, ct);
        if (custodyBalance < request.Quantity)
            throw new ValidationFailedException($"Insufficient custody balance to return (available: {custodyBalance}).");

        var now = DateTime.UtcNow;
        var movement = new StockMovement
        {
            MovementType = StockMovementType.ReturnFromCustody,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity,
            DestinationWarehouseId = request.WarehouseId,
            RepresentativeId = request.RepresentativeId,
            MovementDateUtc = now,
            ReasonCode = request.ReasonCode
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        db.CustodyTransactions.Add(new CustodyTransaction
        {
            RepresentativeId = request.RepresentativeId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            TransactionType = CustodyTransactionType.Returned,
            Quantity = -request.Quantity,
            SourceStockMovementId = movement.Id,
            ReasonCode = request.ReasonCode,
            TransactionDateUtc = now
        });
        await db.SaveChangesAsync(ct);

        return movement.Id;
    }

    public async Task TransferAsync(CustodyTransferRequest request, CancellationToken ct = default)
    {
        if (request.FromRepresentativeId == request.ToRepresentativeId)
            throw new ValidationFailedException("Source and destination representatives must be different.");
        if (request.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        var balance = await GetCustodyBalanceAsync(request.FromRepresentativeId, request.ProductId, request.ProductBatchId, ct);
        if (balance < request.Quantity)
            throw new ValidationFailedException($"Insufficient custody balance to transfer (available: {balance}).");

        var now = DateTime.UtcNow;
        db.CustodyTransactions.Add(new CustodyTransaction
        {
            RepresentativeId = request.FromRepresentativeId,
            CounterpartRepresentativeId = request.ToRepresentativeId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            TransactionType = CustodyTransactionType.TransferOut,
            Quantity = -request.Quantity,
            TransactionDateUtc = now
        });
        db.CustodyTransactions.Add(new CustodyTransaction
        {
            RepresentativeId = request.ToRepresentativeId,
            CounterpartRepresentativeId = request.FromRepresentativeId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            TransactionType = CustodyTransactionType.TransferIn,
            Quantity = request.Quantity,
            TransactionDateUtc = now
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<StockReconciliationResultDto> ReconcileAsync(StockReconciliationRequest request, CancellationToken ct = default)
    {
        var systemBalance = await GetCustodyBalanceAsync(request.RepresentativeId, request.ProductId, request.ProductBatchId, ct);
        var variance = request.CountedBalance - systemBalance;

        var reconciliation = new StockReconciliation
        {
            RepresentativeId = request.RepresentativeId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            ReconciliationDateUtc = DateTime.UtcNow,
            SystemBalance = systemBalance,
            CountedBalance = request.CountedBalance,
            Notes = request.Notes
        };
        db.StockReconciliations.Add(reconciliation);

        if (variance != 0)
        {
            var adjustment = new CustodyTransaction
            {
                RepresentativeId = request.RepresentativeId,
                ProductId = request.ProductId,
                ProductBatchId = request.ProductBatchId,
                TransactionType = variance > 0 ? CustodyTransactionType.AdjustmentIncrease : CustodyTransactionType.AdjustmentDecrease,
                Quantity = variance,
                ReasonCode = "Reconciliation variance",
                TransactionDateUtc = DateTime.UtcNow
            };
            db.CustodyTransactions.Add(adjustment);
            await db.SaveChangesAsync(ct); // need the adjustment's id to link it back on the reconciliation row

            reconciliation.AdjustmentCustodyTransactionId = adjustment.Id;
        }

        await db.SaveChangesAsync(ct);

        return new StockReconciliationResultDto(systemBalance, request.CountedBalance, variance, variance != 0);
    }

    public async Task DeductForSaleAsync(int representativeId, Sale sale, IReadOnlyList<(int ProductId, int Quantity)> lines,
        CancellationToken ct = default)
    {
        // Validate every line's availability up front so a shortfall on a later line never leaves
        // earlier lines partially staged (all-or-nothing, per spec 4.5).
        var plannedDeductions = new List<(int ProductId, int? ProductBatchId, int Quantity)>();

        foreach (var (productId, quantity) in lines)
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Product), productId);

            if (!product.IsBatchTracked)
            {
                var balance = await GetCustodyBalanceAsync(representativeId, productId, null, ct);
                if (balance < quantity)
                    throw new ValidationFailedException(
                        $"'{product.Name}': custody balance ({balance}) is insufficient for this sale ({quantity}).");

                plannedDeductions.Add((productId, null, quantity));
                continue;
            }

            var batchBalances = await GetRepBatchBalancesFefoAsync(representativeId, productId, ct);
            var remaining = quantity;
            foreach (var (batchId, available) in batchBalances)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, available);
                if (take <= 0) continue;
                plannedDeductions.Add((productId, batchId, take));
                remaining -= take;
            }

            if (remaining > 0)
                throw new ValidationFailedException(
                    $"'{product.Name}': custody balance is insufficient for this sale (short by {remaining}).");
        }

        var now = DateTime.UtcNow;
        foreach (var (productId, batchId, quantity) in plannedDeductions)
        {
            db.CustodyTransactions.Add(new CustodyTransaction
            {
                RepresentativeId = representativeId,
                ProductId = productId,
                ProductBatchId = batchId,
                TransactionType = CustodyTransactionType.Sold,
                Quantity = -quantity,
                Sale = sale,
                TransactionDateUtc = now
            });
        }
    }

    public Task<int> GetBalanceAsync(int representativeId, int productId, int? productBatchId, CancellationToken ct = default)
        => GetCustodyBalanceAsync(representativeId, productId, productBatchId, ct);

    public async Task<int> GetProductBalanceAsync(int representativeId, int productId, CancellationToken ct = default)
    {
        return await db.CustodyTransactions
            .Where(c => c.RepresentativeId == representativeId && c.ProductId == productId)
            .SumAsync(c => (int?)c.Quantity, ct) ?? 0;
    }

    public async Task<int> GetAvailableToSellAsync(int representativeId, int productId, CancellationToken ct = default)
    {
        var balance = await GetProductBalanceAsync(representativeId, productId, ct);
        var reserved = await GetReservedQuantityAsync(representativeId, productId, ct);
        return balance - reserved;
    }

    public async Task<IReadOnlyList<RepStockReservationDto>> GetReservationsAsync(int representativeId, CancellationToken ct = default)
    {
        var balances = await db.CustodyTransactions.AsNoTracking()
            .Where(c => c.RepresentativeId == representativeId)
            .GroupBy(c => c.ProductId)
            .Select(g => new { ProductId = g.Key, Balance = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        if (balances.Count == 0) return [];

        var reserved = await db.OrderLines.AsNoTracking()
            .Where(l => !l.IsDeleted && l.Order.RepresentativeId == representativeId && l.Order.Status == OrderStatus.Approved)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Reserved = g.Sum(x => x.Quantity + x.BonusQuantity) })
            .ToListAsync(ct);
        var reservedMap = reserved.ToDictionary(r => r.ProductId, r => r.Reserved);

        var products = await db.Products.AsNoTracking()
            .Where(p => balances.Select(b => b.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return balances
            .Where(b => b.Balance != 0 || reservedMap.ContainsKey(b.ProductId))
            .Select(b =>
            {
                var reservedQty = reservedMap.GetValueOrDefault(b.ProductId, 0);
                return new RepStockReservationDto(b.ProductId, products.GetValueOrDefault(b.ProductId, "?"),
                    b.Balance, reservedQty, b.Balance - reservedQty);
            })
            .OrderBy(r => r.ProductName)
            .ToList();
    }

    public async Task ReceiveCustomerReturnAsync(int representativeId, int productId, int? productBatchId,
        int quantity, string? reasonCode, CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");

        db.CustodyTransactions.Add(new CustodyTransaction
        {
            RepresentativeId = representativeId,
            ProductId = productId,
            ProductBatchId = productBatchId,
            TransactionType = CustodyTransactionType.CustomerReturn,
            Quantity = quantity,
            ReasonCode = reasonCode,
            TransactionDateUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> GetReservedQuantityAsync(int representativeId, int productId, CancellationToken ct)
    {
        return await db.OrderLines
            .Where(l => !l.IsDeleted && l.ProductId == productId && l.Order.RepresentativeId == representativeId
                && l.Order.Status == OrderStatus.Approved)
            .SumAsync(l => (int?)(l.Quantity + l.BonusQuantity), ct) ?? 0;
    }

    private async Task<int> GetCustodyBalanceAsync(int representativeId, int productId, int? batchId, CancellationToken ct)
    {
        return await db.CustodyTransactions
            .Where(c => c.RepresentativeId == representativeId && c.ProductId == productId && c.ProductBatchId == batchId)
            .SumAsync(c => (int?)c.Quantity, ct) ?? 0;
    }

    private async Task<int> GetWarehouseBalanceAsync(int warehouseId, int productId, int? batchId, CancellationToken ct)
    {
        var inbound = await db.StockMovements
            .Where(m => m.DestinationWarehouseId == warehouseId && m.ProductId == productId && m.ProductBatchId == batchId)
            .SumAsync(m => (int?)m.Quantity, ct) ?? 0;
        var outbound = await db.StockMovements
            .Where(m => m.SourceWarehouseId == warehouseId && m.ProductId == productId && m.ProductBatchId == batchId)
            .SumAsync(m => (int?)m.Quantity, ct) ?? 0;
        return inbound - outbound;
    }

    /// <summary>Representative's held batches for a product with a positive balance, earliest expiry first (FEFO).</summary>
    private async Task<List<(int BatchId, int Balance)>> GetRepBatchBalancesFefoAsync(int representativeId, int productId,
        CancellationToken ct)
    {
        var balances = await db.CustodyTransactions.AsNoTracking()
            .Where(c => c.RepresentativeId == representativeId && c.ProductId == productId && c.ProductBatchId != null)
            .GroupBy(c => c.ProductBatchId!.Value)
            .Select(g => new { BatchId = g.Key, Balance = g.Sum(x => x.Quantity) })
            .Where(x => x.Balance > 0)
            .ToListAsync(ct);

        if (balances.Count == 0) return [];

        var expiries = await db.ProductBatches.AsNoTracking()
            .Where(b => balances.Select(x => x.BatchId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.ExpiryDate, ct);

        return balances
            .OrderBy(b => expiries.GetValueOrDefault(b.BatchId, DateOnly.MaxValue))
            .Select(b => (b.BatchId, b.Balance))
            .ToList();
    }

    /// <summary>Warehouse's earliest-expiring batch that alone covers the requested quantity (spec 4.4 FEFO default).</summary>
    private async Task<int> PickFefoWarehouseBatchAsync(int warehouseId, int productId, int quantity, CancellationToken ct)
    {
        var batches = await db.ProductBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && !b.IsDeleted)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);

        foreach (var batch in batches)
        {
            var available = await GetWarehouseBalanceAsync(warehouseId, productId, batch.Id, ct);
            if (available >= quantity) return batch.Id;
        }

        throw new ValidationFailedException(
            "No single batch at this warehouse covers the requested quantity — split the issue across batches manually.");
    }
}

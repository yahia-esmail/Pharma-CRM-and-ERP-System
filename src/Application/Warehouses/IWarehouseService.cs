using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Warehouses;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseListItemDto>> GetListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(WarehouseSaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, WarehouseSaveRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<ProductBatchDto>> GetBatchesAsync(int? productId, CancellationToken ct = default);
    Task<int> CreateBatchAsync(ProductBatchSaveRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<StockLevelDto>> GetStockLevelsAsync(int? warehouseId, int? productId, CancellationToken ct = default);

    Task<PagedResult<StockMovementDto>> GetMovementsAsync(PagedRequest request, int? warehouseId, int? productId,
        CancellationToken ct = default);

    /// <summary>Stock-in from a supplier (spec 4.4) — simplified/manual until Phase 5's Purchase Orders exist.</summary>
    Task<int> ReceiveGoodsAsync(GoodsReceiptRequest request, CancellationToken ct = default);

    Task<int> TransferAsync(StockTransferRequest request, CancellationToken ct = default);

    Task<int> AdjustAsync(StockAdjustmentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<NearExpiryBatchDto>> GetNearExpiryAsync(int thresholdDays, CancellationToken ct = default);

    Task<IReadOnlyList<LowStockProductDto>> GetLowStockAsync(CancellationToken ct = default);
}

namespace PharmaERP.Application.Traceability;

/// <summary>"Trace This Transaction" (spec 4.11) — walks the full chain in both directions from any stock or financial record.</summary>
public interface ITraceService
{
    Task<BatchTraceDto> TraceBatchAsync(int productBatchId, CancellationToken ct = default);
    Task<SaleTraceDto> TraceSaleAsync(int saleId, CancellationToken ct = default);
}

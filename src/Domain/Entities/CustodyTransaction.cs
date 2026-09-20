using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>
/// One entry in a representative's stock custody ledger (spec 4.5). A representative's current balance
/// for a product/batch is always SUM(Quantity) over these rows — never an independently editable field
/// (spec 5.3's central integrity rule). SourceStockMovementId and SaleId carry the traceability chain
/// back to its origin (spec 4.11): a warehouse issue/return, or the sale that consumed the stock.
/// </summary>
public class CustodyTransaction : AuditableEntity
{
    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public CustodyTransactionType TransactionType { get; set; }

    /// <summary>Signed — positive for Received/AdjustmentIncrease/TransferIn, negative otherwise.</summary>
    public int Quantity { get; set; }

    public int? SourceStockMovementId { get; set; }
    public StockMovement? SourceStockMovement { get; set; }

    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    /// <summary>The counterpart representative for TransferIn/TransferOut handover pairs.</summary>
    public int? CounterpartRepresentativeId { get; set; }
    public Representative? CounterpartRepresentative { get; set; }

    public DateTime TransactionDateUtc { get; set; }
    public string? ReasonCode { get; set; }
}

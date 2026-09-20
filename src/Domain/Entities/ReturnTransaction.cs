using PharmaERP.Domain.Common;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Domain.Entities;

/// <summary>A first-class, approval-gated return request (gap-analysis addendum 3.8) — either a pharmacy
/// handing stock back to a representative, or a representative handing stock back to a warehouse. Never
/// applied to Stock Custody / Warehouse Inventory until Approved (see ReturnService.ApproveAsync, which
/// delegates the actual ledger write to ICustodyService so there's exactly one place that writes
/// CustodyTransaction/StockMovement rows).</summary>
public class ReturnTransaction : AuditableEntity
{
    public ReturnFlowType FlowType { get; set; }

    public int RepresentativeId { get; set; }
    public Representative Representative { get; set; } = null!;

    /// <summary>Set only for CustomerToRepresentative.</summary>
    public int? PharmacyId { get; set; }
    public Pharmacy? Pharmacy { get; set; }

    /// <summary>Destination warehouse — set only for RepresentativeToWarehouse.</summary>
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public int Quantity { get; set; }
    public ReturnReason Reason { get; set; }
    public string? Notes { get; set; }

    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;

    public string RequestedByUserId { get; set; } = null!;
    public DateTime RequestedAtUtc { get; set; }

    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}

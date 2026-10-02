using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Returns;

public record ReturnTransactionDto(
    int Id,
    ReturnFlowType FlowType,
    int RepresentativeId,
    string RepresentativeName,
    int? PharmacyId,
    string? PharmacyName,
    int? WarehouseId,
    string? WarehouseName,
    int ProductId,
    string ProductName,
    int? ProductBatchId,
    string? BatchNumber,
    int Quantity,
    ReturnReason Reason,
    string? Notes,
    ReturnStatus Status,
    string RequestedByUserId,
    DateTime RequestedAtUtc,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    string? RejectionReason);

public class ReturnRequestSaveRequest
{
    public ReturnFlowType FlowType { get; set; }
    public int? PharmacyId { get; set; }
    public int? WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public int Quantity { get; set; }
    public ReturnReason Reason { get; set; }
    public string? Notes { get; set; }
}

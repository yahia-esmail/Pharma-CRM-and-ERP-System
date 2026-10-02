using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Collections;

public record CollectionDto(
    int Id,
    int RepresentativeId,
    string RepresentativeName,
    int PharmacyId,
    string PharmacyName,
    int? SaleId,
    decimal Amount,
    DateTime CollectionDateUtc,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    int AttachmentCount);

public record CollectionAttachmentDto(int Id, int CollectionId, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);

public class CollectionSaveRequest
{
    public int PharmacyId { get; set; }
    public int? SaleId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? CollectionDateUtc { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public record RemittanceTransactionDto(
    int Id,
    int RepresentativeId,
    string RepresentativeName,
    decimal Amount,
    DateTime RemittanceDateUtc,
    PaymentMethod RemittanceMethod,
    string ReceivingUserId,
    string? ReferenceNumber);

public class RemittanceSaveRequest
{
    public int RepresentativeId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod RemittanceMethod { get; set; }
    public string? ReferenceNumber { get; set; }
}

/// <summary>Computed, never stored (spec 5.3) — Outstanding = Total Collected − Total Remitted +/- any
/// Approved financial-reconciliation variance (addendum 3.6).</summary>
public record RepFinancialCustodyDto(
    int RepresentativeId,
    string RepresentativeName,
    decimal TotalCollected,
    decimal TotalRemitted,
    decimal OutstandingBalance,
    DateTime? OldestUnremittedCollectionDateUtc);

public record FinancialReconciliationDto(
    int Id,
    int RepresentativeId,
    string RepresentativeName,
    DateTime ReconciliationDateUtc,
    decimal SystemBalance,
    decimal CountedBalance,
    decimal Variance,
    string? Reason,
    FinancialReconciliationStatus Status,
    string RequestedByUserId,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    string? RejectionReason);

public class FinancialReconciliationRequest
{
    public int RepresentativeId { get; set; }
    public decimal CountedBalance { get; set; }
    public string? Reason { get; set; }
}

public record FinancialReconciliationResultDto(int Id, decimal SystemBalance, decimal CountedBalance, decimal Variance, bool RequiresApproval);

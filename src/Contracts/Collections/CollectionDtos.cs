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

public record CollectionAllocationDto(int SaleId, decimal Amount);

public class CollectionAllocationRequest
{
    public int SaleId { get; set; }
    public decimal Amount { get; set; }
}

public class CollectionSaveRequest
{
    public int PharmacyId { get; set; }

    /// <summary>Single-invoice shortcut (older clients). Prefer <see cref="Allocations"/>.</summary>
    public int? SaleId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? CollectionDateUtc { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    /// <summary>How the amount is split across this pharmacy's invoices (wireframe 10). May cover less than the
    /// full amount; the rest is applied to the oldest open invoices.</summary>
    public List<CollectionAllocationRequest>? Allocations { get; set; }
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

/// <summary>A rep's own cash count (field app): the representative is always the caller.</summary>
public class MyFinancialReconciliationRequest
{
    public decimal CountedBalance { get; set; }
    public string? Reason { get; set; }
}

public class FinancialReconciliationRequest
{
    public int RepresentativeId { get; set; }
    public decimal CountedBalance { get; set; }
    public string? Reason { get; set; }
}

public record FinancialReconciliationResultDto(int Id, decimal SystemBalance, decimal CountedBalance, decimal Variance, bool RequiresApproval);

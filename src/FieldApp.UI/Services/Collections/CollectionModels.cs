using PharmaERP.Application.Collections;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Domain.Enums;

namespace PharmaERP.FieldApp.UI.Services.Collections;

/// <summary>A proof-of-payment photo, already downscaled to JPEG on the phone (plan phase 9 sizing).</summary>
public sealed record CollectionPhoto(string Label, string FileName, string ContentType, string Base64)
{
    public long SizeBytes => Base64.Length * 3L / 4;
    public string DataUrl => $"data:{ContentType};base64,{Base64}";
}

public sealed record InvoiceAllocation(int SaleId, decimal Amount);

/// <summary>What the rep enters on the Collection screen (wireframe 10).</summary>
public sealed class CollectionDraft
{
    public Guid LocalId { get; init; } = Guid.NewGuid();
    public int PharmacyId { get; set; }
    public string PharmacyName { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    /// <summary>Explicit split across invoices; empty = the server applies it to the oldest invoices.</summary>
    public List<InvoiceAllocation> Allocations { get; set; } = [];
    public List<CollectionPhoto> Photos { get; set; } = [];
}

/// <summary>A collection on its way to the server (its request and photo uploads are in the outbox).</summary>
public sealed record PendingCollection(
    Guid LocalId,
    Guid CollectionOutboxId,
    IReadOnlyList<Guid> PhotoOutboxIds,
    string PharmacyName,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    DateTime CollectedAtUtc);

/// <summary>Wireframe 5, Financial tab: the server's figures plus the latest collections and cash counts.</summary>
public sealed record FinancialCustodySnapshot(
    RepFinancialCustodyDto Custody,
    IReadOnlyList<CollectionDto> RecentCollections,
    IReadOnlyList<FinancialReconciliationDto> Reconciliations);

public static class CollectionRules
{
    public const int MaxPhotos = 4;

    /// <summary>A cheque or transfer is traced by its number; cash and card slips don't need one.</summary>
    public static bool NeedsReference(PaymentMethod m) => m is PaymentMethod.Cheque or PaymentMethod.BankTransfer;

    public static string ReferenceLabel(PaymentMethod m) => m switch
    {
        PaymentMethod.Cheque => "Cheque number",
        PaymentMethod.BankTransfer => "Transfer reference",
        PaymentMethod.Card => "Card approval code (optional)",
        _ => "Receipt number (optional)"
    };

    /// <summary>The photos to ask for: both sides of a cheque, otherwise the receipt.</summary>
    public static IReadOnlyList<string> PhotoSlots(PaymentMethod m) =>
        m == PaymentMethod.Cheque ? ["Cheque front", "Cheque back"] : ["Receipt"];

    /// <summary>Problems that block sending, mirroring the server's rules (CollectionService) so the rep sees
    /// them before leaving the screen. <paramref name="openInvoices"/> is the cached ledger, when known.</summary>
    public static IReadOnlyList<string> Validate(CollectionDraft d, IReadOnlyList<PharmacyLedgerLineDto>? openInvoices)
    {
        var problems = new List<string>();
        if (d.PharmacyId <= 0) problems.Add("Choose the pharmacy.");
        if (d.Amount <= 0) problems.Add("Enter the amount collected.");
        if (NeedsReference(d.Method) && string.IsNullOrWhiteSpace(d.ReferenceNumber))
            problems.Add($"Enter the {ReferenceLabel(d.Method).ToLowerInvariant()}.");
        var split = d.Allocations.Where(a => a.Amount > 0).ToList();
        if (split.Sum(a => a.Amount) > d.Amount)
            problems.Add("The invoice split adds up to more than the amount collected.");
        if (openInvoices is not null)
            foreach (var a in split)
                if (openInvoices.FirstOrDefault(l => l.SaleId == a.SaleId) is { } line && a.Amount > line.Open)
                    problems.Add($"Invoice #{a.SaleId} only has {line.Open:N2} left to pay.");
        if (d.Photos.Count > MaxPhotos) problems.Add($"At most {MaxPhotos} photos.");
        return problems;
    }

    /// <summary>The order the server applies an unsplit amount in (oldest invoice first) — used to show where
    /// the money will go, and for "fill oldest first".</summary>
    public static IReadOnlyList<InvoiceAllocation> OldestFirst(decimal amount, IEnumerable<PharmacyLedgerLineDto> invoices)
    {
        var result = new List<InvoiceAllocation>();
        foreach (var line in invoices.Where(l => l.Open > 0).OrderBy(l => l.SaleDateUtc).ThenBy(l => l.SaleId))
        {
            if (amount <= 0) break;
            var apply = Math.Min(amount, line.Open);
            result.Add(new InvoiceAllocation(line.SaleId, apply));
            amount -= apply;
        }
        return result;
    }
}

public static class CustodyRules
{
    public const int NearExpiryDays = 60;

    public static bool IsExpired(DateOnly? expiry, DateOnly today) => expiry is { } e && e < today;

    public static bool IsNearExpiry(DateOnly? expiry, DateOnly today) =>
        expiry is { } e && e >= today && e.DayNumber - today.DayNumber <= NearExpiryDays;

    /// <summary>Quantity sign for the ledger: what adds to the rep's custody vs. what takes from it.</summary>
    public static int Sign(CustodyTransactionType t) => t switch
    {
        CustodyTransactionType.Received or CustodyTransactionType.AdjustmentIncrease
            or CustodyTransactionType.TransferIn or CustodyTransactionType.CustomerReturn => 1,
        _ => -1
    };
}

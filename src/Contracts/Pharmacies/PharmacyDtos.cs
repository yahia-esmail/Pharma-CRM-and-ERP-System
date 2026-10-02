using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Pharmacies;

public record PharmacyListItemDto(
    int Id,
    string Name,
    string? City,
    string? Segment,
    string? PrimaryRepresentativeName,
    decimal OutstandingBalance,
    PharmacyStatus Status,
    // Shop position, when known — lets the field app rank "nearby" pharmacies and check geofences offline.
    double? Latitude = null,
    double? Longitude = null);

public record PharmacyDetailDto(
    int Id,
    string Name,
    string? LicenseNumber,
    string? OwnerName,
    string? Address,
    string? Governorate,
    string? City,
    string? Phone,
    string? WhatsAppNumber,
    string? Email,
    string? Segment,
    int PaymentTermDays,
    decimal CreditLimit,
    int? PrimaryRepresentativeId,
    string? PrimaryRepresentativeName,
    int? TerritoryId,
    string? TerritoryName,
    PharmacyStatus Status,
    double? Latitude,
    double? Longitude);

public class PharmacySaveRequest
{
    public string Name { get; set; } = null!;
    public string? LicenseNumber { get; set; }
    public string? OwnerName { get; set; }
    public string? Address { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }
    public string? Segment { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public decimal CreditLimit { get; set; }
    public int? PrimaryRepresentativeId { get; set; }
    public int? TerritoryId { get; set; }
    public PharmacyStatus Status { get; set; } = PharmacyStatus.Prospect;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Computed, never stored (spec 5.3) — total collected is always 0 until Phase 5 adds Collections,
/// at which point outstanding = TotalSales - TotalCollected becomes a real number instead of a stand-in.
/// </summary>
public record PharmacyLedgerDto(
    int PharmacyId,
    decimal TotalSales,
    decimal TotalCollected,
    decimal OutstandingBalance,
    decimal Aging0To30,
    decimal Aging31To60,
    decimal Aging60Plus,
    IReadOnlyList<PharmacyLedgerLineDto> Lines);

public record PharmacyLedgerLineDto(int SaleId, DateTime SaleDateUtc, DateTime DueDateUtc, decimal Amount, int DaysOverdue);

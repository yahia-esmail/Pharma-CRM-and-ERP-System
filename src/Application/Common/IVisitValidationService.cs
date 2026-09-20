namespace PharmaERP.Application.Common;

public record VisitValidationResult(bool LocationMismatch, bool OutsideTerritory, bool DurationTooShort);

/// <summary>Visit Validation engine (gap-analysis addendum 3.3) — evaluates a visit's check-in against
/// configurable company-policy rules at the moment it's submitted. Rule violations never block the save
/// (field connectivity/reality can be messy); they only set flags for District/Sales Managers to see on
/// the visit record and roll up into a Visit Quality indicator on the Performance Dashboard.</summary>
public interface IVisitValidationService
{
    Task<VisitValidationResult> EvaluateAsync(int? representativeTerritoryId, int? customerTerritoryId,
        double? customerLatitude, double? customerLongitude, double? checkInLatitude, double? checkInLongitude,
        int? durationMinutes, CancellationToken ct = default);
}

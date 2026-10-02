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

    /// <summary>Geofence check that gives the rep the benefit of the fix's reported accuracy (capped by
    /// <see cref="VisitValidationOptions.MaxCreditedAccuracyMeters"/>): a ±40 m fix 220 m from the customer
    /// could really be inside a 200 m fence, so it isn't flagged. False when either side has no position.</summary>
    bool IsLocationMismatch(double? customerLatitude, double? customerLongitude,
        double? latitude, double? longitude, double? accuracyMeters);

    Task<bool> IsOutsideTerritoryAsync(int? representativeTerritoryId, int? customerTerritoryId, CancellationToken ct = default);

    bool IsDurationTooShort(int? durationMinutes);
}

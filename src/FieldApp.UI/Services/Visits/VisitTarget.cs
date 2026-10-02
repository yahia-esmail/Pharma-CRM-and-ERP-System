using Microsoft.Extensions.Options;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Plan;

namespace PharmaERP.FieldApp.UI.Services.Visits;

/// <summary>Who is being visited, from the offline data: identity, where they are, and which plan stop
/// (if any) the visit fulfills.</summary>
public sealed record VisitTarget(
    StopKind Kind,
    int Id,
    string Name,
    string? Detail,
    string? Place,
    double? Latitude,
    double? Longitude,
    double GeofenceRadiusMeters,
    int? PlanItemId)
{
    public bool HasLocation => Latitude is not null && Longitude is not null;
    public bool IsPlanned => PlanItemId is not null;
}

public sealed class VisitTargetResolver(MasterDataSync masterData, TodayPlanState plan, IOptions<GpsOptions> gps)
{
    /// <summary>Null when the customer isn't in the offline catalogue (not downloaded yet, or not the rep's).</summary>
    public async Task<VisitTarget?> ResolveAsync(StopKind kind, int customerId, int? planItemId)
    {
        await plan.EnsureLoadedAsync();
        // Opening a planned customer from "Nearby" still counts as the planned visit.
        var stop = plan.Stops.FirstOrDefault(s => s.Item.Id == planItemId)
                   ?? plan.Stops.FirstOrDefault(s => s.Kind == kind && s.CustomerId == customerId && s.Status != StopStatus.Done);

        if (kind == StopKind.Doctor)
        {
            var d = (await masterData.GetDoctorsAsync())?.Data.FirstOrDefault(x => x.Id == customerId);
            if (d is null && stop is null) return null;
            return new VisitTarget(kind, customerId, d?.FullName ?? stop!.Name,
                Join(" · ", d?.Specialty ?? stop?.Item.Specialty, d?.ClassificationName ?? stop?.Item.ClassificationName),
                Join(", ", stop?.Item.Address, d?.City ?? stop?.Item.City),
                d?.Latitude ?? stop?.Item.Latitude, d?.Longitude ?? stop?.Item.Longitude,
                gps.Value.GeofenceRadiusDoctorMeters, stop?.Item.Id);
        }

        var p = (await masterData.GetPharmaciesAsync())?.Data.FirstOrDefault(x => x.Id == customerId);
        if (p is null && stop is null) return null;
        return new VisitTarget(kind, customerId, p?.Name ?? stop!.Name,
            p?.Segment is { } segment ? $"Segment {segment}" : null,
            Join(", ", stop?.Item.Address, p?.City ?? stop?.Item.City),
            p?.Latitude ?? stop?.Item.Latitude, p?.Longitude ?? stop?.Item.Longitude,
            gps.Value.GeofenceRadiusPharmacyMeters, stop?.Item.Id);
    }

    private static string? Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } s ? s : null;
}

using PharmaERP.Domain.Enums;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Plan;

namespace PharmaERP.FieldApp.UI.Services.Visits;

public sealed record SampleLine(int ProductId, string ProductName, int Quantity);

/// <summary>The visit in progress on this phone (plan phase 6). Persisted as soon as the rep checks in, so
/// closing the app — or the phone dying — brings them back to the same open visit with its timer and draft.</summary>
public sealed class OpenVisit
{
    public Guid LocalId { get; init; } = Guid.NewGuid();
    public StopKind Kind { get; init; }
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public int? PlanItemId { get; init; }

    /// <summary>Device clock when the rep tapped Check-in; the server corrects it for clock offset.</summary>
    public DateTime CheckInDeviceUtc { get; init; }
    public GeoFix? CheckInFix { get; init; }
    public GeofenceStatus? CheckInGeofence { get; init; }
    public double? CheckInDistanceMeters { get; init; }
    public string? OutsideReason { get; init; }

    /// <summary>The queued check-in; the check-out depends on it.</summary>
    public Guid CheckInOutboxId { get; init; }

    // Draft — doctor visit (wireframe 7)
    public List<int> ProductsDiscussed { get; set; } = [];
    public List<SampleLine> Samples { get; set; } = [];
    public VisitInterestLevel? InterestLevel { get; set; }
    public string? Notes { get; set; }
    public string? NextVisitRecommendation { get; set; }

    // Draft — pharmacy visit (wireframe 8)
    public PharmacyVisitPurpose Purpose { get; set; } = PharmacyVisitPurpose.OrderTaking;

    public DateTime? DraftSavedAtUtc { get; set; }

    /// <summary>Placeholder the outbox resolves to the server's visit id once the check-in is delivered.</summary>
    public string VisitRef => $"visit:{LocalId}";

    public string Controller => Kind == StopKind.Doctor ? "Doctors" : "Pharmacies";
    public string ScreenUrl => Kind == StopKind.Doctor ? $"visits/doctor/{CustomerId}" : $"visits/pharmacy/{CustomerId}";
}

public static class OutsideGeofenceReasons
{
    public static readonly IReadOnlyList<string> All =
    [
        "Doctor moved clinic / customer relocated",
        "Saved customer location is wrong",
        "GPS inaccurate here (indoors, mall, basement)",
        "Met the customer at another location",
        "Other"
    ];
}

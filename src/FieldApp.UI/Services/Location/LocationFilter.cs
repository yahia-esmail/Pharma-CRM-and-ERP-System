namespace PharmaERP.FieldApp.UI.Services.Location;

public enum FilterDecision
{
    /// <summary>Store it.</summary>
    Keep,
    /// <summary>Too close in space and time to the last kept point — adds nothing to the route.</summary>
    Skip,
    /// <summary>Accuracy worse than the configured limit (e.g. a cell-tower fix indoors).</summary>
    RejectPoorAccuracy,
    /// <summary>Implies an impossible speed from the last kept point. Stored, flagged for the server
    /// (plan 7.8), but not used as the reference for later points.</summary>
    Anomaly
}

/// <summary>Decides which tracking fixes are worth uploading (plan 7.4): enough movement or enough time
/// since the last kept point, acceptable accuracy, and no teleporting.</summary>
public sealed class LocationFilter(GpsOptions options)
{
    private GeoFix? _lastKept;

    public GeoFix? LastKept => _lastKept;

    public FilterDecision Evaluate(GeoFix fix)
    {
        if (fix.AccuracyMeters > options.MaxAcceptedAccuracyMeters) return FilterDecision.RejectPoorAccuracy;
        if (_lastKept is null)
        {
            _lastKept = fix;
            return FilterDecision.Keep;
        }

        var distance = GeoMath.DistanceMeters(_lastKept, fix);
        var seconds = (fix.DeviceTimestampUtc - _lastKept.DeviceTimestampUtc).TotalSeconds;
        if (seconds <= 0) return FilterDecision.Skip;   // duplicate or out-of-order callback

        // Both fixes have error; only movement beyond their combined accuracy counts toward speed,
        // otherwise GPS jitter between two readings a second apart looks like a car chase.
        var credibleDistance = Math.Max(0, distance - fix.AccuracyMeters - _lastKept.AccuracyMeters);
        if (credibleDistance / seconds * 3.6 > options.MaxSpeedKmh) return FilterDecision.Anomaly;

        if (distance < options.TrackDistanceMeters && seconds < options.TrackIntervalSeconds) return FilterDecision.Skip;

        _lastKept = fix;
        return FilterDecision.Keep;
    }

    public void Reset() => _lastKept = null;
}

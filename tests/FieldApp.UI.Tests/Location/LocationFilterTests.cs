using PharmaERP.FieldApp.UI.Services;
using PharmaERP.FieldApp.UI.Services.Location;
using static PharmaERP.FieldApp.UI.Tests.Location.GeoTestData;

namespace PharmaERP.FieldApp.UI.Tests.Location;

public class LocationFilterTests
{
    private readonly LocationFilter _filter = new(new GpsOptions
    {
        MaxAcceptedAccuracyMeters = 100,
        TrackDistanceMeters = 50,
        TrackIntervalSeconds = 180,
        MaxSpeedKmh = 150
    });

    [Fact]
    public void First_acceptable_fix_is_kept()
    {
        Assert.Equal(FilterDecision.Keep, _filter.Evaluate(Fix()));
    }

    [Fact]
    public void Poor_accuracy_is_rejected_and_does_not_become_the_reference()
    {
        Assert.Equal(FilterDecision.RejectPoorAccuracy, _filter.Evaluate(Fix(accuracy: 450)));
        Assert.Null(_filter.LastKept);
    }

    [Fact]
    public void Small_movement_within_the_interval_is_skipped()
    {
        _filter.Evaluate(Fix());
        Assert.Equal(FilterDecision.Skip, _filter.Evaluate(Fix(north: 20, seconds: 30)));
    }

    [Fact]
    public void Moving_fifty_metres_keeps_a_point()
    {
        _filter.Evaluate(Fix());
        Assert.Equal(FilterDecision.Keep, _filter.Evaluate(Fix(north: 60, seconds: 20)));
    }

    [Fact]
    public void Standing_still_still_keeps_a_point_every_interval()
    {
        _filter.Evaluate(Fix());
        Assert.Equal(FilterDecision.Skip, _filter.Evaluate(Fix(north: 5, seconds: 179)));
        Assert.Equal(FilterDecision.Keep, _filter.Evaluate(Fix(north: 5, seconds: 181)));
    }

    [Fact]
    public void Impossible_jump_is_flagged_as_an_anomaly_and_does_not_move_the_reference()
    {
        var start = Fix();
        _filter.Evaluate(start);

        // 5 km in 30 s = 600 km/h.
        Assert.Equal(FilterDecision.Anomaly, _filter.Evaluate(Fix(north: 5000, seconds: 30)));
        Assert.Same(start, _filter.LastKept);
    }

    [Fact]
    public void Gps_jitter_between_close_readings_is_not_mistaken_for_speed()
    {
        _filter.Evaluate(Fix(accuracy: 40));
        // 70 m apart one second later looks like 252 km/h, but both fixes are ±40 m.
        Assert.NotEqual(FilterDecision.Anomaly, _filter.Evaluate(Fix(north: 70, accuracy: 40, seconds: 1)));
    }

    [Fact]
    public void Driving_through_the_city_is_normal()
    {
        _filter.Evaluate(Fix());
        // 1 km in 60 s = 60 km/h.
        Assert.Equal(FilterDecision.Keep, _filter.Evaluate(Fix(north: 1000, seconds: 60)));
    }

    [Fact]
    public void Out_of_order_readings_are_skipped()
    {
        _filter.Evaluate(Fix(seconds: 100));
        Assert.Equal(FilterDecision.Skip, _filter.Evaluate(Fix(north: 500, seconds: 90)));
    }
}

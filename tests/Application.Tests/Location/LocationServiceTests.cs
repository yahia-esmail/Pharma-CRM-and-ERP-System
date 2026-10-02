using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Location;
using PharmaERP.Domain.Entities;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Location;

public class LocationServiceTests : IDisposable
{
    private const int RepId = 7;
    private readonly ApplicationDbContext _db;
    private readonly LocationService _service;

    public LocationServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new LocationService(_db, TestCalendar.Cairo());
    }

    private static LocationPingRequest Point(Guid? clientId = null, double lat = 30.04, double lon = 31.23,
        DateTime? at = null, string? source = "Track", double? accuracy = 12) => new()
    {
        ClientId = clientId ?? Guid.NewGuid(),
        Latitude = lat,
        Longitude = lon,
        TimestampUtc = at ?? DateTime.UtcNow.AddMinutes(-5),
        AccuracyMeters = accuracy,
        Source = source
    };

    [Fact]
    public async Task Batch_is_stored_with_accuracy_source_and_receipt_time()
    {
        var before = DateTime.UtcNow;
        var result = await _service.RecordPingsAsync(RepId, [Point(accuracy: 8), Point(source: "Event")]);

        Assert.Equal(new LocationPingBatchResult(2, 0), result);
        var stored = await _db.LocationPings.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal([8d, 12d], stored.Select(p => p.AccuracyMeters!.Value));
        Assert.Equal(["Track", "Event"], stored.Select(p => p.Source));
        Assert.All(stored, p => Assert.InRange(p.ReceivedAtUtc, before, DateTime.UtcNow));
        Assert.All(stored, p => Assert.True(p.ReceivedAtUtc > p.TimestampUtc));
    }

    [Fact]
    public async Task Resending_a_batch_does_not_store_points_twice()
    {
        var batch = new List<LocationPingRequest> { Point(), Point(), Point() };
        await _service.RecordPingsAsync(RepId, batch);

        var again = await _service.RecordPingsAsync(RepId, [.. batch, Point()]);

        Assert.Equal(new LocationPingBatchResult(1, 3), again);
        Assert.Equal(4, await _db.LocationPings.CountAsync());
    }

    [Fact]
    public async Task Duplicates_inside_one_batch_are_stored_once()
    {
        var id = Guid.NewGuid();
        var result = await _service.RecordPingsAsync(RepId, [Point(id), Point(id)]);

        Assert.Equal(new LocationPingBatchResult(1, 1), result);
    }

    [Fact]
    public async Task The_same_client_id_from_another_rep_is_a_different_point()
    {
        var id = Guid.NewGuid();
        await _service.RecordPingsAsync(RepId, [Point(id)]);

        var other = await _service.RecordPingsAsync(RepId + 1, [Point(id)]);

        Assert.Equal(1, other.Accepted);
    }

    [Fact]
    public async Task Legacy_ping_without_the_new_fields_still_works()
    {
        await _service.RecordPingAsync(RepId, new LocationPingRequest { Latitude = 30, Longitude = 31 });

        var ping = await _db.LocationPings.SingleAsync();
        Assert.Null(ping.AccuracyMeters);
        Assert.Null(ping.ClientId);
        Assert.Equal(LocationPingSources.Track, ping.Source);
        Assert.Equal(ping.ReceivedAtUtc, ping.TimestampUtc);
    }

    [Theory]
    [InlineData(91, 31, "latitude")]
    [InlineData(30, -181, "longitude")]
    public async Task Coordinates_out_of_range_are_rejected(double lat, double lon, string field)
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.RecordPingsAsync(RepId, [Point(), Point(lat: lat, lon: lon)]));

        Assert.Contains("Point 2", ex.Message);
        Assert.Contains(field, ex.Message);
        Assert.Equal(0, await _db.LocationPings.CountAsync());   // all-or-nothing
    }

    [Fact]
    public async Task A_fix_from_the_future_points_at_a_wrong_device_clock()
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.RecordPingsAsync(RepId, [Point(at: DateTime.UtcNow.AddHours(2))]));

        Assert.Contains("device clock", ex.Message);
    }

    [Fact]
    public async Task Points_queued_offline_for_a_few_days_are_accepted()
    {
        var result = await _service.RecordPingsAsync(RepId, [Point(at: DateTime.UtcNow.AddDays(-3))]);
        Assert.Equal(1, result.Accepted);
    }

    [Fact]
    public async Task Unknown_source_and_oversized_batches_are_rejected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.RecordPingsAsync(RepId, [Point(source: "Teleport")]));

        var tooMany = Enumerable.Range(0, LocationPingBatchRequest.MaxPoints + 1).Select(_ => Point()).ToList();
        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.RecordPingsAsync(RepId, tooMany));
    }

    public void Dispose() => _db.Dispose();
}

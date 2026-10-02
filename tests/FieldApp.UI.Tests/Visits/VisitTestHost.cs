using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using PharmaERP.FieldApp.UI.Services;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Location;
using PharmaERP.FieldApp.UI.Services.Collections;
using PharmaERP.FieldApp.UI.Services.Offline;
using PharmaERP.FieldApp.UI.Services.Orders;
using PharmaERP.FieldApp.UI.Services.Plan;
using PharmaERP.FieldApp.UI.Services.Storage;
using PharmaERP.FieldApp.UI.Services.Visits;
using PharmaERP.FieldApp.UI.Tests.Location;
using PharmaERP.FieldApp.UI.Tests.Offline;

namespace PharmaERP.FieldApp.UI.Tests.Visits;

/// <summary>The visit flow wired end to end: KeyValueStore over an in-memory db.js, the real outbox and
/// processor against the scripted API, plan state and tracker. <see cref="Restart"/> simulates closing
/// and reopening the app (fresh services, same storage).</summary>
internal sealed class VisitTestHost
{
    private readonly OutboxTestHost _outbox = new();

    public FakeDbJs Js { get; } = new();
    public KeyValueStore Storage { get; }
    public FakeApi Api => _outbox.Api;
    public InMemoryOutboxStore OutboxStore => _outbox.Store;
    public OutboxProcessor Processor => _outbox.Processor;
    public Outbox Outbox => _outbox.Outbox;
    public Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock => _outbox.Clock;

    public MasterDataSync MasterData { get; private set; } = null!;
    public TodayPlanState Plan { get; private set; } = null!;
    public VisitSessionManager Visits { get; private set; } = null!;
    public PharmacyAccountCache Accounts { get; private set; } = null!;
    public OrderEntryManager Orders { get; private set; } = null!;
    public OrdersStore OrdersStore { get; private set; } = null!;
    public CollectionEntryManager Collections { get; private set; } = null!;

    public VisitTestHost()
    {
        Storage = new KeyValueStore(new LocalDb(Js));
        Restart();
    }

    public void Restart()
    {
        var http = new HttpClient(Api, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") };
        var pharmacies = new PharmaciesApi(http);
        MasterData = new MasterDataSync(Storage, new ProductsApi(http), new DoctorsApi(http), pharmacies,
            new VisitPlansApi(http), new CustodyApi(http), Clock, NullLogger<MasterDataSync>.Instance);
        var tracker = new LocationTracker(new FakeLocationService(), new InMemoryTrackerStorage(), _outbox.Outbox,
            Options.Create(new GpsOptions()), Clock, NullLogger<LocationTracker>.Instance);
        Plan = new TodayPlanState(MasterData, Storage, tracker, Clock);
        Visits = new VisitSessionManager(Storage, _outbox.Outbox, Plan, MasterData, tracker, Clock);
        Accounts = new PharmacyAccountCache(pharmacies, Storage, Clock, NullLogger<PharmacyAccountCache>.Instance);
        Orders = new OrderEntryManager(Storage, _outbox.Outbox, _outbox.Store, Clock);
        OrdersStore = new OrdersStore(new OrdersApi(http), Storage, Clock, NullLogger<OrdersStore>.Instance);
        Collections = new CollectionEntryManager(Storage, _outbox.Outbox, Clock);
    }

    public Task SeedAsync<T>(string dataset, params T[] items) =>
        Storage.SetAsync(StorageKeys.MasterData(dataset), new Cached<IReadOnlyList<T>>(items, Clock.GetUtcNow().UtcDateTime));
}

/// <summary>In-memory stand-in for wwwroot/js/db.js: values are the JSON strings LocalDb serializes.</summary>
internal sealed class FakeDbJs : IJSRuntime, IJSObjectReference
{
    public Dictionary<(string Store, string Key), string> Data { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        object? result = identifier switch
        {
            "import" => this,
            "get" => Data.GetValueOrDefault(((string)args![0]!, (string)args[1]!)),
            "put" => Put((string)args![0]!, (string)args[1]!, (string)args[2]!),
            "remove" => Data.Remove(((string)args![0]!, (string)args[1]!)),
            "getAll" => Data.Where(e => e.Key.Store == (string)args![0]!).Select(e => e.Value).ToArray(),
            "clearAll" => Clear(),
            _ => throw new NotSupportedException(identifier)
        };
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    private object? Put(string store, string key, string json)
    {
        Data[(store, key)] = json;
        return null;
    }

    private object? Clear()
    {
        Data.Clear();
        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

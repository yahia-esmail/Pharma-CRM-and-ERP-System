using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.FieldApp.UI.Services.Offline;

namespace PharmaERP.FieldApp.UI.Tests.Offline;

/// <summary>Outbox + processor wired to an in-memory store, a fake API and a controllable clock.</summary>
internal sealed class OutboxTestHost
{
    public InMemoryOutboxStore Store { get; } = new();
    public FakeApi Api { get; } = new();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero));
    public Outbox Outbox { get; }
    public OutboxProcessor Processor { get; }

    public OutboxTestHost()
    {
        Outbox = new Outbox(Store, Clock);
        Processor = new OutboxProcessor(Store, Outbox, new FakeHttpClientFactory(Api), Clock,
            NullLogger<OutboxProcessor>.Instance);
    }

    public Task<OutboxItem> EnqueueAsync(string url, object? body = null, Guid? dependsOn = null, string? producesRef = null) =>
        Outbox.EnqueueAsync(new OutboxRequest("Test", $"POST {url}", "POST", url, body, dependsOn, producesRef));
}

internal sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly Dictionary<Guid, OutboxItem> _items = [];
    private readonly Dictionary<string, int> _refs = [];

    public IReadOnlyList<OutboxItem> Items => _items.Values.OrderBy(i => i.Sequence).ToList();

    public Task<IReadOnlyList<OutboxItem>> GetAllAsync() => Task.FromResult(Items);
    public Task SaveAsync(OutboxItem item) { _items[item.Id] = item; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id) { _items.Remove(id); return Task.CompletedTask; }
    public Task<int?> GetRefAsync(string reference) => Task.FromResult(_refs.TryGetValue(reference, out var id) ? id : (int?)null);
    public Task SaveRefAsync(string reference, int serverId) { _refs[reference] = serverId; return Task.CompletedTask; }
}

internal sealed record RecordedRequest(string Method, string Path, string? IdempotencyKey, string? Body);

/// <summary>Scripted API: responses are queued per path; unscripted paths answer 201 with a new id.</summary>
internal sealed class FakeApi : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _scripts = [];
    private int _nextId = 100;

    public List<RecordedRequest> Requests { get; } = [];
    public bool Offline { get; set; }

    public void Respond(string path, params Func<HttpResponseMessage>[] responses)
    {
        if (!_scripts.TryGetValue(path, out var queue)) _scripts[path] = queue = new();
        foreach (var r in responses) queue.Enqueue(r);
    }

    public static HttpResponseMessage Status(HttpStatusCode code, string? json = null)
    {
        var response = new HttpResponseMessage(code);
        if (json is not null) response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Offline) throw new HttpRequestException("offline");

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        Requests.Add(new RecordedRequest(request.Method.Method, path,
            request.Headers.TryGetValues(OutboxProcessor.IdempotencyHeader, out var keys) ? keys.Single() : null,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));

        if (_scripts.TryGetValue(path, out var queue) && queue.Count > 0) return queue.Dequeue()();
        return Status(HttpStatusCode.Created, $"{{\"id\":{_nextId++}}}");
    }
}

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") };
}

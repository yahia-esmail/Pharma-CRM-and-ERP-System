using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PharmaERP.FieldApp.UI.Services.Api;

namespace PharmaERP.FieldApp.UI.Services.Offline;

/// <param name="Sent">Items delivered in this run.</param>
/// <param name="StoppedForConnectivity">The run stopped early because the API was unreachable.</param>
/// <param name="NextDueUtc">Earliest scheduled retry among remaining items, if any.</param>
public sealed record OutboxRunResult(int Sent, bool StoppedForConnectivity, DateTime? NextDueUtc);

/// <summary>Delivers outbox items oldest-first (plan 8.2).
///
/// Outcome per response:
/// - 2xx → delivered: removed, and its returned <c>id</c> recorded if it <c>ProducesRef</c>.
/// - network error / timeout / 408 / 429 / 5xx / 401 → transient: exponential backoff, and the run stops
///   (retrying the rest against a dead connection only drains the battery).
/// - 409 "still processing" (idempotency) → retried after the server's Retry-After.
/// - any other 4xx → <see cref="OutboxStatus.NeedsAttention"/>, never retried automatically, since
///   resending the same invalid request can only fail the same way.
/// A child waits while its parent is still in the outbox, including when the parent needs attention.</summary>
public sealed partial class OutboxProcessor(
    IOutboxStore store,
    Outbox outbox,
    IHttpClientFactory httpClientFactory,
    TimeProvider time,
    ILogger<OutboxProcessor> logger)
{
    public const string HttpClientName = "PharmaApi.Outbox";
    public const string IdempotencyHeader = "Idempotency-Key";
    public const string ClientSentAtHeader = "X-Client-Sent-At";

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)
    ];

    private readonly SemaphoreSlim _runLock = new(1, 1);

    public bool IsRunning { get; private set; }

    public async Task<OutboxRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        await _runLock.WaitAsync(ct);
        IsRunning = true;
        try
        {
            var items = (await store.GetAllAsync()).OrderBy(i => i.Sequence).ToList();
            var queued = items.Select(i => i.Id).ToHashSet();
            var sent = 0;
            DateTime? nextDue = null;

            foreach (var item in items)
            {
                if (item.Status == OutboxStatus.NeedsAttention) continue;
                if (item.DependsOn is { } parent && queued.Contains(parent)) continue;

                var now = time.GetUtcNow().UtcDateTime;
                if (item.NextAttemptAtUtc is { } due && due > now)
                {
                    nextDue = Min(nextDue, due);
                    continue;
                }

                switch (await DeliverAsync(item, ct))
                {
                    case Delivery.Delivered:
                        queued.Remove(item.Id);
                        sent++;
                        break;
                    case Delivery.Rescheduled:
                        nextDue = Min(nextDue, item.NextAttemptAtUtc);
                        break;
                    case Delivery.Unreachable:
                        nextDue = Min(nextDue, item.NextAttemptAtUtc);
                        return new OutboxRunResult(sent, true, nextDue);
                }
            }

            return new OutboxRunResult(sent, false, nextDue);
        }
        finally
        {
            IsRunning = false;
            _runLock.Release();
        }
    }

    private enum Delivery { Delivered, Rescheduled, Unreachable, Rejected }

    private async Task<Delivery> DeliverAsync(OutboxItem item, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        item.Attempts++;
        item.LastAttemptAtUtc = now;

        var url = await ResolveRefsAsync(item.Url);
        var body = item.JsonBody is null ? null : await ResolveRefsAsync(item.JsonBody);
        if ((url.Missing ?? body?.Missing) is { } missing)
            return await RejectAsync(item, null, $"The server id for '{missing}' is unknown, so this can't be sent.");

        using var request = new HttpRequestMessage(new HttpMethod(item.Method), url.Value);
        request.Headers.Add(IdempotencyHeader, item.Id.ToString());
        // The device clock at the moment of *this* attempt (the body may have been queued hours ago offline):
        // the server compares it with its own clock to correct device timestamps (see IFieldVisit).
        request.Headers.Add(ClientSentAtHeader, time.GetUtcNow().UtcDateTime.ToString("O"));
        if (item.FileKey is { } fileKey)
        {
            if (await store.GetFileAsync(fileKey) is not { } file)
                return await RejectAsync(item, null, "The photo is no longer on this phone, so it can't be uploaded.");
            var part = new ByteArrayContent(Convert.FromBase64String(file.Base64));
            part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            request.Content = new MultipartFormDataContent { { part, "file", file.FileName } };
        }
        else if (body is not null)
            request.Content = new StringContent(body.Value, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            await ScheduleRetryAsync(item, null, "Server unreachable — will retry automatically.");
            return Delivery.Unreachable;
        }

        using (response)
        {
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                if (item.ProducesRef is { } reference)
                {
                    if (await ReadIdAsync(response, ct) is { } serverId)
                        await store.SaveRefAsync(reference, serverId);
                    else
                        logger.LogWarning("Outbox item {Kind} {Id} succeeded but returned no id for {Ref}", item.Kind, item.Id, reference);
                }
                await store.DeleteAsync(item.Id);
                if (item.FileKey is { } delivered) await store.DeleteFileAsync(delivered);
                outbox.NotifyChanged();
                return Delivery.Delivered;
            }

            if (response.StatusCode == HttpStatusCode.Conflict && response.Headers.RetryAfter?.Delta is { } retryAfter)
            {
                // Idempotency middleware: an earlier attempt with this key is still running server-side.
                await ScheduleRetryAsync(item, status, "Still being processed by the server.", retryAfter);
                return Delivery.Rescheduled;
            }

            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                or HttpStatusCode.Unauthorized || status >= 500)
            {
                var message = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Session expired — sign in again to sync."
                    : $"Server error ({status}) — will retry automatically.";
                await ScheduleRetryAsync(item, status, message, response.Headers.RetryAfter?.Delta);
                return Delivery.Unreachable;
            }

            return await RejectAsync(item, status, await ReadProblemAsync(response, ct));
        }
    }

    private async Task ScheduleRetryAsync(OutboxItem item, int? status, string message, TimeSpan? delay = null)
    {
        var wait = delay ?? Backoff[Math.Min(item.Attempts, Backoff.Length) - 1];
        item.NextAttemptAtUtc = time.GetUtcNow().UtcDateTime + wait;
        item.LastStatusCode = status;
        item.LastError = message;
        await store.SaveAsync(item);
        outbox.NotifyChanged();
    }

    private async Task<Delivery> RejectAsync(OutboxItem item, int? status, string message)
    {
        item.Status = OutboxStatus.NeedsAttention;
        item.NextAttemptAtUtc = null;
        item.LastStatusCode = status;
        item.LastError = message;
        await store.SaveAsync(item);
        outbox.NotifyChanged();
        logger.LogWarning("Outbox item {Kind} {Id} rejected ({Status}): {Message}", item.Kind, item.Id, status, message);
        return Delivery.Rejected;
    }

    [GeneratedRegex(@"\{ref:(?<ref>[^}]+)\}")]
    private static partial Regex RefPlaceholder();

    private sealed record Resolved(string Value, string? Missing);

    private async Task<Resolved> ResolveRefsAsync(string text)
    {
        var matches = RefPlaceholder().Matches(text);
        if (matches.Count == 0) return new(text, null);

        var result = new StringBuilder(text);
        foreach (var reference in matches.Select(m => m.Groups["ref"].Value).Distinct())
        {
            if (await store.GetRefAsync(reference) is not { } id) return new(text, reference);
            var placeholder = OutboxRefs.Placeholder(reference);
            // In a JSON body the placeholder was serialized as a string value ("orderId":"{ref:…}");
            // ids are ints, so replace it together with its quotes to produce a JSON number.
            result.Replace($"\"{placeholder}\"", id.ToString());
            result.Replace(placeholder, id.ToString());
        }
        return new(result.ToString(), null);
    }

    /// <summary>Create endpoints return <c>{ "id": 123 }</c> (see e.g. OrdersController.Create).</summary>
    private static async Task<int?> ReadIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Object when doc.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var v) => v,
                JsonValueKind.Number when doc.RootElement.TryGetInt32(out var n) => n,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Turns an RFC 7807 ProblemDetails (what Web.Api returns for 4xx) into one readable line.</summary>
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var fallback = $"Rejected by the server ({(int)response.StatusCode} {response.ReasonPhrase}).";
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return fallback;

            var parts = new List<string>();
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t) parts.Add(t);
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d) parts.Add(d);
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                parts.AddRange(errors.EnumerateObject()
                    .SelectMany(e => e.Value.ValueKind == JsonValueKind.Array ? e.Value.EnumerateArray() : Enumerable.Empty<JsonElement>())
                    .Select(m => m.GetString())
                    .OfType<string>());
            }
            return parts.Count > 0 ? string.Join(" — ", parts.Distinct()) : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static DateTime? Min(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;
}

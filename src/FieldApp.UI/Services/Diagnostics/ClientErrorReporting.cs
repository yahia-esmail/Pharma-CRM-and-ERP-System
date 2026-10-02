using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PharmaERP.Application.Diagnostics;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Auth;
using PharmaERP.FieldApp.UI.Services.Device;

namespace PharmaERP.FieldApp.UI.Services.Diagnostics;

/// <summary>Collects errors logged anywhere in the app — including Blazor's "unhandled exception rendering
/// component" — for <see cref="ClientErrorReporter"/> to send to the API (plan phase 11). Anything that could
/// identify a person or a session is scrubbed first.</summary>
public sealed partial class ClientErrorSink : ILoggerProvider
{
    private const int MaxQueued = 50;
    private static readonly ConcurrentQueue<ClientErrorEntry> Queue = new();

    /// <summary>The screen the rep is on (path only), kept current by the layout.</summary>
    public static string? CurrentPath { get; set; }

    public static int Count => Queue.Count;

    public ILogger CreateLogger(string categoryName) =>
        // Our own sending, and HTTP noise (an offline phone fails requests all day), are never reported.
        categoryName.StartsWith("PharmaERP.FieldApp.UI.Services.Diagnostics", StringComparison.Ordinal)
        || categoryName.StartsWith("System.Net.Http", StringComparison.Ordinal)
        || categoryName.StartsWith("Microsoft.Extensions.Http", StringComparison.Ordinal)
            ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
            : new SinkLogger(categoryName);

    public static IReadOnlyList<ClientErrorEntry> Take(int max)
    {
        var batch = new List<ClientErrorEntry>();
        while (batch.Count < max && Queue.TryDequeue(out var e)) batch.Add(e);
        return batch;
    }

    internal static void Add(ClientErrorEntry entry)
    {
        Queue.Enqueue(entry);
        while (Queue.Count > MaxQueued) Queue.TryDequeue(out _);   // a burst keeps the newest
    }

    /// <summary>Removes tokens, e-mail addresses and query strings (which may carry ids or search terms).</summary>
    public static string Scrub(string text)
    {
        text = Bearer().Replace(text, "Bearer ***");
        text = Jwt().Replace(text, "***jwt***");
        text = Email().Replace(text, "***@***");
        return QueryString().Replace(text, "?…");
    }

    public void Dispose() { }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_\.=]+", RegexOptions.IgnoreCase)] private static partial Regex Bearer();
    [GeneratedRegex(@"eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+")] private static partial Regex Jwt();
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")] private static partial Regex Email();
    [GeneratedRegex(@"\?[^\s""'<>]+")] private static partial Regex QueryString();

    private sealed class SinkLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Add(new ClientErrorEntry
            {
                OccurredAtUtc = DateTime.UtcNow,
                Level = logLevel.ToString(),
                Category = category,
                Message = Scrub(formatter(state, exception)),
                Exception = exception is null ? null : Scrub(exception.ToString()),
                Path = CurrentPath
            });
        }
    }
}

/// <summary>Sends collected errors to the API every so often, only when signed in and online. Best effort: a
/// batch that can't be sent is dropped rather than retried forever (the next error will be reported anyway).</summary>
public sealed class ClientErrorReporter(ClientErrorsApi api, TokenStore tokens, INetworkStatus network,
    ILogger<ClientErrorReporter> logger)
{
    private static readonly string AppVersion =
        typeof(ClientErrorReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    private PeriodicTimer? _timer;

    public void Start(TimeSpan? interval = null)
    {
        if (_timer is not null) return;
        _timer = new PeriodicTimer(interval ?? TimeSpan.FromSeconds(20));
        _ = LoopAsync(_timer);
    }

    private async Task LoopAsync(PeriodicTimer timer)
    {
        while (await timer.WaitForNextTickAsync()) await FlushAsync();
    }

    public async Task<int> FlushAsync()
    {
        if (ClientErrorSink.Count == 0 || tokens.Session is null || !network.IsOnline) return 0;
        var batch = ClientErrorSink.Take(20);
        try
        {
            await api.ReportAsync(new ClientErrorReport { AppVersion = AppVersion, Errors = [.. batch] });
            return batch.Count;
        }
        catch (Exception ex)
        {
            logger.LogInformation("Client errors not reported: {Error}", ex.Message);
            return 0;
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PharmaERP.FieldApp.UI.Services.Api;
using PharmaERP.FieldApp.UI.Services.Auth;
using PharmaERP.FieldApp.UI.Services.Device;
using PharmaERP.FieldApp.UI.Services.Diagnostics;
using PharmaERP.FieldApp.UI.Tests.Visits;
using PharmaERP.Web.Api.Contracts;

namespace PharmaERP.FieldApp.UI.Tests.Diagnostics;

// The sink's queue is static (one per app); these tests share it, so they run one at a time.
[Collection(nameof(ClientErrorReportingTests))]
public class ClientErrorReportingTests
{
    public ClientErrorReportingTests() => ClientErrorSink.Take(1000);

    [Fact]
    public void Tokens_emails_and_query_strings_are_scrubbed()
    {
        var scrubbed = ClientErrorSink.Scrub(
            "POST https://api.test/api/v1/Orders?pharmacyId=5&q=Dr%20Karim failed for yahia123@gmail.com " +
            "with Authorization: Bearer abc.DEF-123 and token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl");

        Assert.DoesNotContain("yahia123@gmail.com", scrubbed);
        Assert.DoesNotContain("abc.DEF-123", scrubbed);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", scrubbed);
        Assert.DoesNotContain("pharmacyId=5", scrubbed);
        Assert.Contains("api/v1/Orders?…", scrubbed);
    }

    [Fact]
    public void Only_errors_are_collected_and_http_noise_is_left_out()
    {
        using var sink = new ClientErrorSink();
        ClientErrorSink.CurrentPath = "/orders/new";

        sink.CreateLogger("PharmaERP.FieldApp.UI.Pages.Orders").LogWarning("just a warning");
        sink.CreateLogger("System.Net.Http.HttpClient.Default").LogError("offline again");
        sink.CreateLogger("Microsoft.AspNetCore.Components.WebAssembly.Rendering.WebAssemblyRenderer")
            .LogCritical(new InvalidOperationException("boom for a@b.com"), "Unhandled exception rendering component");

        var entry = Assert.Single(ClientErrorSink.Take(10));
        Assert.Equal(("Critical", "/orders/new"), (entry.Level, entry.Path));
        Assert.Contains("InvalidOperationException", entry.Exception);
        Assert.DoesNotContain("a@b.com", entry.Exception);
    }

    [Fact]
    public void A_burst_keeps_only_the_newest_fifty()
    {
        var logger = new ClientErrorSink().CreateLogger("PharmaERP.FieldApp.UI.Test");
        for (var i = 0; i < 80; i++) logger.LogError("error {N}", i);

        Assert.Equal(50, ClientErrorSink.Count);
        Assert.Equal("error 30", ClientErrorSink.Take(1)[0].Message);
    }

    [Fact]
    public async Task Errors_are_sent_only_once_signed_in()
    {
        var host = new VisitTestHost();
        var http = new HttpClient(host.Api, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") };
        var tokens = new TokenStore(host.Storage);
        var reporter = new ClientErrorReporter(new ClientErrorsApi(http), tokens, new Online(), NullLogger<ClientErrorReporter>.Instance);
        new ClientErrorSink().CreateLogger("PharmaERP.FieldApp.UI.Test").LogError("first");

        Assert.Equal(0, await reporter.FlushAsync());   // not signed in: kept

        await tokens.SaveAsync(new LoginResponse("access", DateTime.UtcNow.AddMinutes(30), "refresh", "Yahia", ["Representative"]));
        Assert.Equal(1, await reporter.FlushAsync());

        var request = Assert.Single(host.Api.Requests);
        Assert.Equal("api/v1/ClientErrors", request.Path);
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal("first", body.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    private sealed class Online : INetworkStatus
    {
        public bool IsOnline => true;
        public event Action<bool>? Changed { add { } remove { } }
        public Task StartAsync() => Task.CompletedTask;
    }
}

[CollectionDefinition(nameof(ClientErrorReportingTests), DisableParallelization = true)]
public class ClientErrorReportingCollection;

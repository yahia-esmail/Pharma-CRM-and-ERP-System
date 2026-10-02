using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PharmaERP.Infrastructure.Storage;

public static class HostingExtensions
{
    /// <summary>Behind a reverse proxy (nginx on Linux), the client IP and scheme come from X-Forwarded-For/-Proto.
    /// They are trusted only from loopback (the framework default) and the addresses listed in
    /// ReverseProxy:KnownProxies — never from the internet. Under IIS the ASP.NET Core Module passes them already.</summary>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));
        });
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace PharmaERP.Infrastructure.Security;

/// <summary>Baseline security headers for go-live hardening (spec 5.4/6) — shared by both web front ends.</summary>
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
            await next();
        });
    }
}

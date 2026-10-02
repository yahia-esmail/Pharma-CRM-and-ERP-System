using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PharmaERP.Application;
using PharmaERP.Application.Common;
using PharmaERP.Infrastructure;
using PharmaERP.Infrastructure.BackgroundServices;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Infrastructure.Security;
using PharmaERP.Infrastructure.Services;
using PharmaERP.Infrastructure.Storage;
using PharmaERP.Web.Api;
using PharmaERP.Web.Api.Middleware;
using PharmaERP.Web.Api.Security;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

// Shares the key ring with Web.Mvc so ASP.NET Core Identity tokens (e.g. password reset) stay
// valid across both front ends, and so keys persist reliably instead of relying on OS-specific
// profile storage that proved unstable during scaffolding (see Web.Mvc's Program.cs).
builder.Services.AddDataProtection()
    .SetApplicationName("PharmaErp")
    .PersistKeysToFileSystem(new DirectoryInfo(StoragePaths.Keys(builder.Configuration, builder.Environment)));

builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(
    StoragePaths.Uploads(builder.Configuration, builder.Environment)));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
// Never from appsettings.json: a secret store, or a key generated into .keys/ in development (see JwtSigningKey).
var signingKey = JwtSigningKey.Resolve(builder.Configuration, builder.Environment);
builder.Services.PostConfigure<JwtSettings>(settings => settings.Key = signingKey);   // TokenService signs with the same key

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization(options => options.AddPharmaPolicies());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Pharma CRM/ERP API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token."
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    // The reference must be bound to the generated document, otherwise it serializes as an empty
    // requirement ("security": [{}]) and Swagger UI never sends the Bearer header.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), [] }
    });
});

// The field-force PWA (PharmaERP.FieldApp) is served from its own origin and calls this API cross-origin.
const string FieldAppCorsPolicy = "FieldApp";
builder.Services.AddCors(options => options.AddPolicy(FieldAppCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:FieldAppOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location", IdempotencyMiddleware.ReplayedHeaderName)));

// Purges stored responses for Idempotency-Key replays (see IdempotencyMiddleware).
builder.Services.AddHostedService<IdempotencyCleanupService>();

// Rate limits (plan 9.4): password guessing on sign-in (per client IP — generous enough for a whole office
// signing in at 8:00 behind one NAT), and runaway location uploads (per user). 429 carries Retry-After; the
// field app's outbox treats it as "try again later".
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        return ValueTask.CompletedTask;
    };
    options.AddPolicy(RateLimits.SignIn, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(RateLimits.PerUser, http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

// Passkeys (plan 9.3). The ceremony runs in the field app's page, on its own origin — so the relying party is the
// app's domain, and the origins accepted are the field app's (Cors:FieldAppOrigins), not this API's.
var fieldAppOrigins = builder.Configuration.GetSection("Cors:FieldAppOrigins").Get<string[]>() ?? [];
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PasskeyFlows>();
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ServerDomain = builder.Configuration["Passkeys:ServerDomain"]
        ?? (fieldAppOrigins.Length > 0 ? new Uri(fieldAppOrigins[0]).Host : null);
    options.ValidateOrigin = context => ValueTask.FromResult(!context.CrossOrigin
        && fieldAppOrigins.Contains(context.Origin, StringComparer.OrdinalIgnoreCase));
});

// Behind a reverse proxy: the real client IP (the sign-in rate limit keys on it) and scheme (see HostingExtensions).
builder.Services.ConfigureForwardedHeaders(builder.Configuration);

builder.Services.AddResponseCompression();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseSecurityHeaders();
app.UseHttpsRedirection();

app.UseCors(FieldAppCorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();   // after authentication, so per-user limits know the user
app.UseAuthorization();
// After authorization: only requests that will actually reach an endpoint are recorded.
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

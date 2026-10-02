using System.Text;
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
using PharmaERP.Web.Api.Middleware;

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
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".keys")));

builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".uploads")));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

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
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSettings.Key)),
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

app.UseResponseCompression();
app.UseSecurityHeaders();
app.UseHttpsRedirection();

app.UseCors(FieldAppCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
// After authorization: only requests that will actually reach an endpoint are recorded.
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

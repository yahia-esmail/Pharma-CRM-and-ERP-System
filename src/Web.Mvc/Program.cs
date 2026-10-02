using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application;
using PharmaERP.Application.Common;
using PharmaERP.Infrastructure;
using PharmaERP.Infrastructure.BackgroundServices;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Infrastructure.Security;
using PharmaERP.Infrastructure.Services;
using PharmaERP.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

// Explicit key persistence — without it, antiforgery/cookie decryption became unreliable across
// requests in some hosting environments (default OS-profile-based key storage isn't always usable).
builder.Services.AddDataProtection()
    .SetApplicationName("PharmaErp")
    .PersistKeysToFileSystem(new DirectoryInfo(StoragePaths.Keys(builder.Configuration, builder.Environment)));

// Collection-attachment/expense-receipt uploads (addendum 3.5/3.9) — a private folder outside wwwroot,
// shared with Web.Api the same way the DataProtection key ring is above.
builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(
    StoragePaths.Uploads(builder.Configuration, builder.Environment)));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options => options.AddPharmaPolicies());

// Registered only here (not in Web.Api) so exactly one process runs the notification scan even though
// both front ends share the same database (addendum 3.10).
builder.Services.AddHostedService<NotificationScanService>();

// Behind a reverse proxy: the real scheme for HTTPS redirection and secure cookies (see HostingExtensions).
builder.Services.ConfigureForwardedHeaders(builder.Configuration);

builder.Services.AddResponseCompression();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

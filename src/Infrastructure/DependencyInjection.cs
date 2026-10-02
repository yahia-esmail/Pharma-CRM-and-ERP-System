using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Identity;
using PharmaERP.Infrastructure.Notifications;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Infrastructure.Persistence.Interceptors;
using PharmaERP.Infrastructure.Security;
using PharmaERP.Infrastructure.Services;

namespace PharmaERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                   .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                // Version3 adds AspNetUserPasskeys: fingerprint / face sign-in on the field app (WebAuthn passkeys).
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        // Web Push (plan 10.4): every stored notification is also pushed to the recipient's devices, in the background.
        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));
        services.AddSingleton<VapidKeyProvider>();
        services.AddSingleton<WebPushQueue>();
        services.AddSingleton<PharmaERP.Application.Notifications.IPushNotifier>(sp => sp.GetRequiredService<WebPushQueue>());
        services.AddHostedService<WebPushDispatchService>();

        return services;
    }
}

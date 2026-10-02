using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PharmaERP.Infrastructure.Storage;

/// <summary>Where the API and the dashboard keep files they must share: the Data Protection key ring (plus the
/// generated VAPID / development JWT keys) and uploaded attachments. Both apps must point at the same folders.
/// Development default: .keys/ and .uploads/ at the repository root (git-ignored). On a server, set
/// Storage:KeysPath and Storage:UploadsPath to folders outside the published sites, so a redeploy keeps them.</summary>
public static class StoragePaths
{
    public static string Keys(IConfiguration configuration, IHostEnvironment environment) =>
        Resolve(configuration["Storage:KeysPath"], environment, ".keys");

    public static string Uploads(IConfiguration configuration, IHostEnvironment environment) =>
        Resolve(configuration["Storage:UploadsPath"], environment, ".uploads");

    private static string Resolve(string? configured, IHostEnvironment environment, string devFolder) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "..", "..", devFolder)
            : configured);
}

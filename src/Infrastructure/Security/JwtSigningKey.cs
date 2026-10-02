using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PharmaERP.Infrastructure.Storage;

namespace PharmaERP.Infrastructure.Security;

/// <summary>Where the JWT signing key comes from. Anyone holding it can mint a token for any user and role, so it
/// must never live in appsettings.json (the repository is public):
///   1. Jwt:Key from a secret store — user-secrets in development, an environment variable (Jwt__Key) or a vault
///      in production;
///   2. Development only, when not configured: a random key generated once into .keys/jwt-signing.key (git-ignored).
/// Production refuses to start without a configured key, and every environment refuses a key that has been
/// published.</summary>
public static class JwtSigningKey
{
    /// <summary>SHA-256 (Base64) of keys known to be public — the one committed in the first commit of the repo.</summary>
    private static readonly HashSet<string> Published = ["YNna1BKAOlnR1AB0ggLJuo3DnCZRSDeqK8CWaxNOqr0="];

    private const int MinimumBytes = 32;   // HMAC-SHA256

    public static string Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[$"{JwtSettings.SectionName}:Key"];
        if (!string.IsNullOrWhiteSpace(configured)) return Validate(configured.Trim(), "Jwt:Key");

        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it from a secret store (e.g. the environment variable Jwt__Key) — " +
                "a Base64 string of at least 32 random bytes. It must never be stored in appsettings.json.");

        var file = Path.Combine(StoragePaths.Keys(configuration, environment), "jwt-signing.key");
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            try
            {
                using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write);
                stream.Write(Encoding.ASCII.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))));
            }
            catch (IOException) when (File.Exists(file)) { }
        }
        return Validate(File.ReadAllText(file).Trim(), file);
    }

    private static string Validate(string key, string source)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(key); }
        catch (FormatException) { throw new InvalidOperationException($"The JWT signing key from {source} is not valid Base64."); }

        if (bytes.Length < MinimumBytes)
            throw new InvalidOperationException($"The JWT signing key from {source} is too short ({bytes.Length} bytes; at least {MinimumBytes}).");
        if (Published.Contains(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)))))
            throw new InvalidOperationException(
                $"The JWT signing key from {source} was published in the source repository and can't be trusted. Generate a new one.");
        return key;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PharmaERP.Infrastructure.Security;

namespace PharmaERP.Application.Tests.Security;

public class JwtSigningKeyTests : IDisposable
{
    // ContentRoot is two levels below the folder that holds .keys (src/Web.Api → repo root), as in the real app.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "jwtkey-" + Guid.NewGuid().ToString("N"));
    private string ContentRoot => Path.Combine(_root, "src", "Web.Api");

    public JwtSigningKeyTests() => Directory.CreateDirectory(ContentRoot);

    private static IConfiguration Config(string? key) =>
        new ConfigurationBuilder().AddInMemoryCollection(key is null ? [] : new Dictionary<string, string?> { ["Jwt:Key"] = key }).Build();

    private IHostEnvironment Env(string name) => new Env_(name, ContentRoot);

    [Fact]
    public void Development_without_a_key_generates_one_and_keeps_it_out_of_the_repository()
    {
        var first = JwtSigningKey.Resolve(Config(null), Env(Environments.Development));
        var second = JwtSigningKey.Resolve(Config(null), Env(Environments.Development));

        Assert.Equal(first, second);   // stable across restarts
        Assert.True(Convert.FromBase64String(first).Length >= 32);
        Assert.True(File.Exists(Path.Combine(_root, ".keys", "jwt-signing.key")));
    }

    [Fact]
    public void Production_refuses_to_start_without_a_configured_key() =>
        Assert.Contains("Jwt:Key is not configured",
            Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Resolve(Config(null), Env(Environments.Production))).Message);

    [Fact]
    public void A_configured_key_is_used_as_is()
    {
        var key = Convert.ToBase64String(new byte[48].Select((_, i) => (byte)(i * 7)).ToArray());
        Assert.Equal(key, JwtSigningKey.Resolve(Config(key), Env(Environments.Production)));
    }

    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("c2hvcnQ=")]   // "short": 5 bytes
    public void Weak_or_malformed_keys_are_refused(string key) =>
        Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Resolve(Config(key), Env(Environments.Production)));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private sealed class Env_(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

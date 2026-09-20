using PharmaERP.Application.Common;

namespace PharmaERP.Infrastructure.Services;

/// <summary>Writes uploaded files to a private folder outside wwwroot (shared between Web.Mvc and
/// Web.Api, mirroring the DataProtection key-ring folder convention) — never directly web-servable;
/// every download goes through an authorized controller action that streams the bytes.</summary>
public class LocalFileStorageService(string rootPath) : IFileStorageService
{
    public async Task<string> SaveAsync(string category, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var relativePath = Path.Combine(category, safeName).Replace('\\', '/');
        var fullPath = Path.Combine(rootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return relativePath;
    }

    public Task<Stream> OpenAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public void Delete(string relativePath)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }
}

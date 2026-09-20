namespace PharmaERP.Application.Common;

/// <summary>Blob/file storage abstraction (addendum 3.5/3.9's "local file storage or cloud object
/// storage" note) — local disk today, swappable for a cloud-backed implementation later without any
/// Application-layer change.</summary>
public interface IFileStorageService
{
    /// <summary>Saves content under the given category (e.g. "collections/42") and returns a relative
    /// path that can be passed back to OpenAsync/Delete later.</summary>
    Task<string> SaveAsync(string category, string fileName, Stream content, CancellationToken ct = default);

    Task<Stream> OpenAsync(string relativePath, CancellationToken ct = default);

    void Delete(string relativePath);
}

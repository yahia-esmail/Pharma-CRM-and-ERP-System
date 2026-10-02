namespace PharmaERP.Application.Diagnostics;

/// <summary>Errors the field app hit on a phone (plan phase 11), sent in small batches. Carries no request
/// bodies or tokens — only what's needed to find the bug.</summary>
public class ClientErrorReport
{
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }
    public List<ClientErrorEntry> Errors { get; set; } = [];
}

public class ClientErrorEntry
{
    public DateTime OccurredAtUtc { get; set; }
    public string Level { get; set; } = "Error";
    public string? Category { get; set; }
    public string Message { get; set; } = "";
    public string? Exception { get; set; }

    /// <summary>The screen (path only, no query string).</summary>
    public string? Path { get; set; }
}

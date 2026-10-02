namespace PharmaERP.FieldApp.UI.Services.Offline;

public enum OutboxStatus
{
    /// <summary>Waiting to be sent (or re-sent after <see cref="OutboxItem.NextAttemptAtUtc"/>).</summary>
    Pending,

    /// <summary>The API rejected it (validation, business rule, forbidden). Never retried automatically —
    /// the rep has to review it on the Pending sync screen.</summary>
    NeedsAttention
}

/// <summary>One queued mutation (plan 8.2). Every write goes through the outbox, online or not, so a
/// dropped connection mid-request can never lose an operation. Successful items are deleted.
///
/// <see cref="Id"/> doubles as the Idempotency-Key header, identical on every attempt, which is what makes
/// resending after a lost response safe on the server side.
///
/// Dependencies: an item created offline can't know the server id of its parent (e.g. lines of an order
/// that was itself created offline). The parent sets <see cref="ProducesRef"/> (say "order:{guid}"); the
/// child sets <see cref="DependsOn"/> and uses the placeholder <c>{ref:order:{guid}}</c> in its URL or body,
/// which is replaced with the parent's returned <c>id</c> before sending.</summary>
public sealed class OutboxItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Machine-readable operation, e.g. "CreateOrder", "SaveCollection".</summary>
    public string Kind { get; init; } = "";

    /// <summary>What the rep sees on the Pending sync screen, e.g. "Order for El-Ezaby Pharmacy".</summary>
    public string Title { get; init; } = "";

    public string Method { get; init; } = "POST";
    public string Url { get; init; } = "";
    public string? JsonBody { get; init; }

    /// <summary>A file sent as multipart/form-data (field "file") instead of a JSON body — e.g. a proof-of-payment
    /// photo. The bytes are stored on the phone next to the item until it is delivered or discarded. With a file,
    /// <see cref="JsonBody"/> (if any) is a flat object whose properties are sent as extra form fields.</summary>
    public string? FileKey { get; init; }

    public Guid? DependsOn { get; init; }
    public string? ProducesRef { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Insertion order; items are sent oldest first.</summary>
    public long Sequence { get; init; }

    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
}

/// <summary>What a feature hands to <see cref="Outbox.EnqueueAsync"/>.</summary>
public sealed record OutboxRequest(
    string Kind,
    string Title,
    string Method,
    string Url,
    object? Body = null,
    Guid? DependsOn = null,
    string? ProducesRef = null,
    Guid? Id = null,
    OutboxFile? File = null);

/// <summary>A file queued for upload (bytes as Base64, so it round-trips through IndexedDB like everything else).</summary>
public sealed record OutboxFile(string FileName, string ContentType, string Base64);

public static class OutboxRefs
{
    /// <summary>The placeholder a dependent item embeds, e.g. <c>Placeholder("order:…")</c> → "{ref:order:…}".</summary>
    public static string Placeholder(string reference) => $"{{ref:{reference}}}";
}

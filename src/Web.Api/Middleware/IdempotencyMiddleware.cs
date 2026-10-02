using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Domain.Entities;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Web.Api.Middleware;

/// <summary>Makes mutating requests safe to retry (plan 6.1 item 3). When a signed-in client sends an
/// <c>Idempotency-Key</c> header on POST/PUT/PATCH/DELETE, the first request runs normally and its
/// response is stored; any later request with the same key gets that stored response replayed instead
/// of executing again. The field app's outbox sends its item id as the key on every attempt.
///
/// - Same key + different method/path/body → 422 (a client bug, never silently replayed).
/// - Same key while the first request is still running → 409 with Retry-After.
/// - 5xx results are not stored, so the client may retry them.
/// Records live in their own DbContext scope so they can never be flushed together with (or roll back)
/// the business changes made by the endpoint.</summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    private const int MaxKeyLength = 100;
    private const int MaxStoredBodyBytes = 1024 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!IsMutating(request.Method)
            || !request.Headers.TryGetValue(HeaderName, out var keyValues)
            || context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString().Trim();
        if (key.Length is 0 or > MaxKeyLength)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                $"{HeaderName} must be 1–{MaxKeyLength} characters.");
            return;
        }

        var path = request.Path + request.QueryString;
        var requestHash = await HashRequestAsync(request, path);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, context.RequestAborted);
        if (existing is not null)
        {
            await RespondToDuplicateAsync(context, existing, requestHash);
            return;
        }

        var record = new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            Method = request.Method,
            Path = path.Length > 500 ? path[..500] : path,
            RequestHash = requestHash,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.IdempotencyRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            // Lost the race against a concurrent delivery of the same key (unique index).
            await WriteInProgressAsync(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            await ForgetAsync(db, record);
            throw;
        }

        context.Response.Body = originalBody;
        buffer.Position = 0;

        if (context.Response.StatusCode >= 500 || buffer.Length > MaxStoredBodyBytes)
        {
            await ForgetAsync(db, record);
        }
        else
        {
            record.StatusCode = context.Response.StatusCode;
            record.ResponseContentType = context.Response.ContentType;
            record.ResponseLocation = context.Response.Headers.Location.ToString() is { Length: > 0 and <= 1000 } location ? location : null;
            record.ResponseBody = buffer.ToArray();
            record.CompletedAtUtc = DateTime.UtcNow;
            // Not RequestAborted: the work is done, so the outcome must be recorded even if the client
            // hung up — that is exactly the lost-response case the replay exists for.
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static bool IsMutating(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static async Task<string> HashRequestAsync(HttpRequest request, string path)
    {
        request.EnableBuffering();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(System.Text.Encoding.UTF8.GetBytes($"{request.Method} {path}\n"));

        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
            sha.AppendData(chunk, 0, read);
        request.Body.Position = 0;

        return Convert.ToBase64String(sha.GetHashAndReset());
    }

    private async Task RespondToDuplicateAsync(HttpContext context, IdempotencyRecord existing, string requestHash)
    {
        if (existing.RequestHash != requestHash)
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                $"This {HeaderName} was already used for a different request.");
            return;
        }

        if (existing.CompletedAtUtc is null)
        {
            await WriteInProgressAsync(context);
            return;
        }

        logger.LogInformation("Replaying stored response for idempotency key {Key} ({Method} {Path})",
            existing.Key, existing.Method, existing.Path);

        var response = context.Response;
        response.StatusCode = existing.StatusCode ?? StatusCodes.Status200OK;
        response.Headers[ReplayedHeaderName] = "true";
        if (existing.ResponseContentType is not null) response.ContentType = existing.ResponseContentType;
        if (existing.ResponseLocation is not null) response.Headers.Location = existing.ResponseLocation;
        if (existing.ResponseBody is { Length: > 0 } body)
            await response.Body.WriteAsync(body, context.RequestAborted);
    }

    private static Task WriteInProgressAsync(HttpContext context)
    {
        context.Response.Headers.RetryAfter = "2";
        return WriteProblemAsync(context, StatusCodes.Status409Conflict,
            "A request with this Idempotency-Key is still being processed.");
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title },
            options: null, contentType: "application/problem+json");
    }

    // Removing the placeholder lets the client retry after a crash or 5xx instead of getting 409 forever.
    private async Task ForgetAsync(ApplicationDbContext db, IdempotencyRecord record)
    {
        try
        {
            db.IdempotencyRecords.Remove(record);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not remove idempotency placeholder {Key}; it will expire with the retention window", record.Key);
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Common;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Central EF Core SaveChanges interception point for the AuditLog table (spec 4.11) — implemented
/// here, not scattered across controllers/services, so no write path can bypass logging. Also stamps
/// CreatedBy/CreatedAt/ModifiedBy/ModifiedAt on every AuditableEntity.
///
/// Newly inserted rows don't have their database-generated key yet at SavingChanges time, so their
/// AuditLog rows are built in SavedChanges (after the key is fixed up) and persisted with a small
/// follow-up SaveChanges call — otherwise the audit trail would record the wrong (temporary) id.
/// Registered as scoped, so the pending list below is safely per-request/per-DbContext-lifetime.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    private readonly List<(EntityEntry Entry, Dictionary<string, object?> NewValues)> _pendingInserts = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProcessChangeTracker(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ProcessChangeTracker(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushPendingInserts(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await FlushPendingInsertsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessChangeTracker(DbContext? context)
    {
        if (context is null) return;

        _pendingInserts.Clear();
        var auditEntries = new List<AuditLog>();
        var utcNow = DateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;

            if (entry.Entity is AuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedByUserId = userId ?? "system";
                        auditable.CreatedAtUtc = utcNow;
                        break;
                    case EntityState.Modified:
                        auditable.ModifiedByUserId = userId;
                        auditable.ModifiedAtUtc = utcNow;
                        break;
                }
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var (oldValues, newValues) = CaptureValues(entry);

            if (entry.State == EntityState.Added)
            {
                // Database-generated key isn't known yet — defer until SavedChanges.
                _pendingInserts.Add((entry, newValues));
                continue;
            }

            if (entry.State == EntityState.Modified && oldValues.Count == 0)
                continue; // no actual scalar changes (e.g. only a navigation fixup)

            auditEntries.Add(new AuditLog
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = ResolveKey(entry),
                Action = entry.State == EntityState.Deleted ? AuditAction.Delete : AuditAction.Update,
                PerformedByUserId = userId,
                TimestampUtc = utcNow,
                OldValuesJson = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                NewValuesJson = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ResolveIpAddress()
            });
        }

        foreach (var auditEntry in auditEntries)
            context.Set<AuditLog>().Add(auditEntry);
    }

    private void FlushPendingInserts(DbContext? context)
    {
        if (context is null || _pendingInserts.Count == 0) return;

        BuildInsertAuditLogs(context);
        context.SaveChanges();
        _pendingInserts.Clear();
    }

    private async Task FlushPendingInsertsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || _pendingInserts.Count == 0) return;

        BuildInsertAuditLogs(context);
        await context.SaveChangesAsync(cancellationToken);
        _pendingInserts.Clear();
    }

    private void BuildInsertAuditLogs(DbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var userId = currentUser.UserId;
        var ipAddress = ResolveIpAddress();

        foreach (var (entry, newValues) in _pendingInserts)
        {
            context.Set<AuditLog>().Add(new AuditLog
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = ResolveKey(entry),
                Action = AuditAction.Create,
                PerformedByUserId = userId,
                TimestampUtc = utcNow,
                NewValuesJson = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ipAddress
            });
        }
    }

    private string? ResolveIpAddress() => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private static (Dictionary<string, object?> OldValues, Dictionary<string, object?> NewValues) CaptureValues(
        EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey()) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[property.Metadata.Name] = property.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[property.Metadata.Name] = property.OriginalValue;
                    break;
                case EntityState.Modified when property.IsModified &&
                                                !Equals(property.OriginalValue, property.CurrentValue):
                    oldValues[property.Metadata.Name] = property.OriginalValue;
                    newValues[property.Metadata.Name] = property.CurrentValue;
                    break;
            }
        }

        return (oldValues, newValues);
    }

    private static string ResolveKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return "unknown";

        var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");
        return string.Join(",", values);
    }
}

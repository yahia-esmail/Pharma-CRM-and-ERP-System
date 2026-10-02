using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Infrastructure.BackgroundServices;

/// <summary>Purges stored idempotency responses once they are too old to be retried. The retention has to
/// outlast the longest time a representative might stay offline with queued operations (refresh tokens
/// last 14 days, so a 30-day window leaves ample margin).</summary>
public class IdempotencyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cutoff = DateTime.UtcNow - Retention;
                var deleted = await db.IdempotencyRecords
                    .Where(r => r.CreatedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Purged {Count} expired idempotency records", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Idempotency cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

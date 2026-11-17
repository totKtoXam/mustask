using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mustask.Application.Abstractions;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images;

/// <summary>
/// Removes the unbound image uploads older than <see cref="ImageLimits.UnboundLifetime"/> (feat-078 BR-IM-16, US-5, plan-backend § 8.2):
/// once at start, then every <see cref="ImageLimits.SweepInterval"/>. It touches no work item row. Registered as a singleton and as
/// a hosted service (the <c>RealmReadinessMonitor</c> pattern), so a test calls <see cref="RunOnceAsync"/> without waiting for a tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>Batches never wait.</b> Each batch deletes the due rows it can lock (<c>FOR UPDATE SKIP LOCKED</c>), so it never holds one lock
/// while waiting for another: a save that is binding a row, a purge that is cascading the item's images and an upload that is
/// inserting beside it cannot make a cycle with it.
/// </para>
/// <para>
/// <b>The skipped rows are decided one at a time.</b> What is still due after the batches is what someone else held. Each of those is
/// deleted by its own statement, which waits for the holder holding nothing itself and re-checks the row on the holder's commit: a
/// row a save has just bound is kept, a row a purge has just removed is gone. So a row due at the start of a run is decided by the
/// end of it, and the three-hour bound holds.
/// </para>
/// <para>The one log line carries a count and nothing else: no id, no user, no item.</para>
/// </remarks>
public sealed class UnboundImageSweeper(
    IServiceScopeFactory scopes,
    ImagesOptions options,
    TimeProvider timeProvider,
    ILogger<UnboundImageSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Sweep.Enabled)
        {
            return;
        }

        try
        {
            await RunSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(ImageLimits.SweepInterval, timeProvider);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failed run is tried again on the next tick; it must not stop the host (the default of a background service).
            logger.LogWarning(exception, "Image sweep failed; it is tried again at the next tick");
        }
    }

    /// <summary>One run. Returns the number of uploads removed.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var cutoff = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow - ImageLimits.UnboundLifetime;
        var batch = Math.Max(1, options.Sweep.BatchSize);
        var removed = 0;

        // 1. Batches, each its own statement and transaction, skipping what another transaction holds.
        int deleted;

        do
        {
            deleted = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DELETE FROM images.images WHERE id IN (
                     SELECT id FROM images.images
                     WHERE bound_at IS NULL AND uploaded_at <= {cutoff}
                     ORDER BY uploaded_at, id
                     LIMIT {batch}
                     FOR UPDATE SKIP LOCKED)
                 """,
                cancellationToken);
            removed += deleted;
        }
        while (deleted > 0);

        // 2. What is still due was held by someone else: a plain read lists it (it never waits), then one row per statement.
        var skipped = await context.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM images.images WHERE bound_at IS NULL AND uploaded_at <= {cutoff} ORDER BY uploaded_at, id")
            .ToListAsync(cancellationToken);

        foreach (var id in skipped)
        {
            removed += await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM images.images WHERE id = {id} AND bound_at IS NULL AND uploaded_at <= {cutoff}",
                cancellationToken);
        }

        logger.LogInformation("Image sweep removed {Count} unbound uploads", removed);

        return removed;
    }
}

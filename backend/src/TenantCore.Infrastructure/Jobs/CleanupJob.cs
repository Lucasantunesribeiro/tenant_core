using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Observability;

namespace TenantCore.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class CleanupJob(
    TenantCoreDbContext dbContext,
    IClock clock,
    ILogger<CleanupJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = TenantCoreTelemetry.ActivitySource.StartActivity(nameof(CleanupJob));
        var now = clock.UtcNow;
        var staleTokens = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(x => x.ExpiresAtUtc < now || (x.RevokedAtUtc.HasValue && x.RevokedAtUtc < now.AddDays(-30)))
            .ToListAsync(context.CancellationToken);

        var staleSnapshots = await dbContext.TenantUsageSnapshots
            .IgnoreQueryFilters()
            .Where(x => x.CapturedAtUtc < now.AddDays(-90))
            .ToListAsync(context.CancellationToken);

        if (staleTokens.Count > 0)
        {
            dbContext.RefreshTokens.RemoveRange(staleTokens);
        }

        if (staleSnapshots.Count > 0)
        {
            dbContext.TenantUsageSnapshots.RemoveRange(staleSnapshots);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        activity?.SetTag("stale.token.count", staleTokens.Count);
        activity?.SetTag("stale.snapshot.count", staleSnapshots.Count);
        logger.LogInformation(
            "CleanupJob removed {TokenCount} refresh tokens and {SnapshotCount} snapshots.",
            staleTokens.Count,
            staleSnapshots.Count);
    }
}

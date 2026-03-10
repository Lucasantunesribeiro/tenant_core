using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Entities;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Observability;

namespace TenantCore.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class UsageSnapshotJob(
    TenantCoreDbContext dbContext,
    IClock clock,
    ICacheService cacheService,
    ILogger<UsageSnapshotJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = TenantCoreTelemetry.ActivitySource.StartActivity(nameof(UsageSnapshotJob));
        var tenantIds = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(context.CancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var now = clock.UtcNow;
            var activeUsers = await dbContext.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && x.IsActive, context.CancellationToken);
            var projects = await dbContext.Projects.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, context.CancellationToken);
            var tasks = await dbContext.Tasks.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, context.CancellationToken);
            var clients = await dbContext.Clients.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, context.CancellationToken);

            var snapshot = new TenantUsageSnapshot(tenantId, now, activeUsers, projects, tasks, clients);
            await dbContext.TenantUsageSnapshots.AddAsync(snapshot, context.CancellationToken);
            await cacheService.RemoveAsync($"usage:{tenantId}", context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        activity?.SetTag("tenant.count", tenantIds.Count);
        logger.LogInformation("UsageSnapshotJob completed for {TenantCount} tenants.", tenantIds.Count);
    }
}

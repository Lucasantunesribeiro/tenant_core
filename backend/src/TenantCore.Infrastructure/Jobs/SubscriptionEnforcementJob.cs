using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Observability;

namespace TenantCore.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class SubscriptionEnforcementJob(
    TenantCoreDbContext dbContext,
    IClock clock,
    ICacheService cacheService,
    ILogger<SubscriptionEnforcementJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = TenantCoreTelemetry.ActivitySource.StartActivity(nameof(SubscriptionEnforcementJob));
        var subscriptions = await dbContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .ToListAsync(context.CancellationToken);

        var plans = await dbContext.SubscriptionPlans.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, context.CancellationToken);

        foreach (var subscription in subscriptions)
        {
            var tenantId = subscription.TenantId;
            var users = await dbContext.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && x.IsActive, context.CancellationToken);
            var projects = await dbContext.Projects.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, context.CancellationToken);
            var clients = await dbContext.Clients.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, context.CancellationToken);
            var plan = plans[subscription.PlanCode];

            var state = QuotaState.Healthy;
            string? message = null;

            if (users > plan.MaxUsers || projects > plan.MaxProjects || clients > plan.MaxClients)
            {
                state = QuotaState.Exceeded;
                message = "One or more plan quotas are currently exceeded.";
            }
            else if (
                users >= Math.Ceiling(plan.MaxUsers * 0.8m) ||
                projects >= Math.Ceiling(plan.MaxProjects * 0.8m) ||
                clients >= Math.Ceiling(plan.MaxClients * 0.8m))
            {
                state = QuotaState.NearLimit;
                message = "Usage is approaching plan limits.";
            }

            subscription.SetQuotaState(state, message, clock.UtcNow);
            await cacheService.RemoveAsync($"subscription:{tenantId}", context.CancellationToken);
            await cacheService.RemoveAsync($"usage:{tenantId}", context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        activity?.SetTag("tenant.count", subscriptions.Count);
        logger.LogInformation("SubscriptionEnforcementJob completed for {TenantCount} tenants.", subscriptions.Count);
    }
}

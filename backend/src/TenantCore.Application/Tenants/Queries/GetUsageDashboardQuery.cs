using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;

namespace TenantCore.Application.Tenants.Queries;

public sealed record GetUsageDashboardQuery : IRequest<UsageDashboardResponse>;

public sealed record UsageDashboardResponse(
    UsageOverview Usage,
    TenantPlanSummary Plan);

internal sealed class GetUsageDashboardQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    ICacheService cacheService) : IRequestHandler<GetUsageDashboardQuery, UsageDashboardResponse>
{
    public async Task<UsageDashboardResponse> Handle(GetUsageDashboardQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();
        var tenantId = currentSession.GetRequiredTenantId();
        var cacheKey = $"usage:{tenantId}";

        var cached = await cacheService.GetAsync<UsageDashboardResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId, cancellationToken);

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleAsync(x => x.Code == subscription.PlanCode, cancellationToken);

        var activeUsers = await dbContext.Users.CountAsync(x => x.IsActive, cancellationToken);
        var projects = await dbContext.Projects.CountAsync(cancellationToken);
        var tasks = await dbContext.Tasks.CountAsync(cancellationToken);
        var clients = await dbContext.Clients.CountAsync(cancellationToken);
        var lastSnapshot = await dbContext.TenantUsageSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.CapturedAtUtc)
            .Select(x => (DateTimeOffset?)x.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var response = new UsageDashboardResponse(
            new UsageOverview(activeUsers, projects, tasks, clients, lastSnapshot),
            new TenantPlanSummary(
                plan.Code,
                plan.Name,
                plan.MaxUsers,
                plan.MaxProjects,
                plan.MaxClients,
                subscription.QuotaState,
                subscription.WarningMessage));

        await cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(2), cancellationToken);
        return response;
    }
}

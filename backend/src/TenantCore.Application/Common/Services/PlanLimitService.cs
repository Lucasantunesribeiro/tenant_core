using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Common.Services;

public sealed class PlanLimitService(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IPlanLimitService
{
    public async Task EnsureUserSlotAvailableAsync(CancellationToken cancellationToken)
    {
        var (plan, usage) = await GetPlanAndUsageAsync(cancellationToken);
        EnsureLimit(usage.ActiveUsers, plan.MaxUsers, "user seats", plan.Code);
    }

    public async Task EnsureProjectSlotAvailableAsync(CancellationToken cancellationToken)
    {
        var (plan, usage) = await GetPlanAndUsageAsync(cancellationToken);
        EnsureLimit(usage.Projects, plan.MaxProjects, "projects", plan.Code);
    }

    public async Task EnsureClientSlotAvailableAsync(CancellationToken cancellationToken)
    {
        var (plan, usage) = await GetPlanAndUsageAsync(cancellationToken);
        EnsureLimit(usage.Clients, plan.MaxClients, "clients", plan.Code);
    }

    private async Task<(SubscriptionPlan plan, (int ActiveUsers, int Projects, int Clients) usage)> GetPlanAndUsageAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = currentSession.GetRequiredTenantId();
        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("subscription_missing", "Subscription missing", 404, "No subscription was found for the tenant.");

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleAsync(x => x.Code == subscription.PlanCode, cancellationToken);

        var activeUsers = await dbContext.Users.CountAsync(x => x.IsActive, cancellationToken);
        var projects = await dbContext.Projects.CountAsync(cancellationToken);
        var clients = await dbContext.Clients.CountAsync(cancellationToken);

        return (plan, (activeUsers, projects, clients));
    }

    private static void EnsureLimit(int currentUsage, int limit, string resourceName, PlanCode planCode)
    {
        if (currentUsage >= limit)
        {
            throw new AppException(
                "plan_limit_exceeded",
                "Plan limit reached",
                422,
                $"The tenant has reached the {resourceName} limit for the {planCode} plan.");
        }
    }
}

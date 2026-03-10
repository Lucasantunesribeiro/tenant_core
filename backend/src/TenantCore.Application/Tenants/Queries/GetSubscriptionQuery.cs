using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tenants.Queries;

public sealed record GetSubscriptionQuery : IRequest<SubscriptionResponse>;

public sealed record SubscriptionResponse(
    PlanCode PlanCode,
    string PlanName,
    string Description,
    int MaxUsers,
    int MaxProjects,
    int MaxClients,
    QuotaState QuotaState,
    string? WarningMessage,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset NextRenewalAtUtc);

internal sealed class GetSubscriptionQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    ICacheService cacheService) : IRequestHandler<GetSubscriptionQuery, SubscriptionResponse>
{
    public async Task<SubscriptionResponse> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();
        var tenantId = currentSession.GetRequiredTenantId();
        var cacheKey = $"subscription:{tenantId}";

        var cached = await cacheService.GetAsync<SubscriptionResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("subscription_missing", "Subscription missing", 404, "No subscription was found for the tenant.");

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleAsync(x => x.Code == subscription.PlanCode, cancellationToken);

        var response = new SubscriptionResponse(
            plan.Code,
            plan.Name,
            plan.Description,
            plan.MaxUsers,
            plan.MaxProjects,
            plan.MaxClients,
            subscription.QuotaState,
            subscription.WarningMessage,
            subscription.RenewedAtUtc,
            subscription.NextRenewalAtUtc);

        await cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }
}

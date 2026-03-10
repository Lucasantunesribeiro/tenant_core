using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tenants.Commands;

public sealed record ChangePlanCommand(PlanCode PlanCode) : IRequest;

internal sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.PlanCode).IsInEnum();
    }
}

internal sealed class ChangePlanCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<ChangePlanCommand>
{
    public async Task Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAdmin();
        var tenantId = currentSession.GetRequiredTenantId();

        var subscription = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("subscription_missing", "Subscription missing", 404, "No subscription was found for the tenant.");

        var planExists = await dbContext.SubscriptionPlans.AnyAsync(x => x.Code == request.PlanCode, cancellationToken);
        if (!planExists)
        {
            throw new AppException("plan_not_found", "Plan not found", 404, "The selected plan does not exist.");
        }

        subscription.ChangePlan(request.PlanCode, clock.UtcNow);

        await auditService.WriteAsync("subscription.plan_changed", "TenantSubscription", subscription.Id.ToString(), new { request.PlanCode }, cancellationToken);
        await cacheService.RemoveAsync($"subscription:{tenantId}", cancellationToken);
        await cacheService.RemoveAsync($"usage:{tenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

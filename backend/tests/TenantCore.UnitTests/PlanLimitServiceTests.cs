using FluentAssertions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Common.Services;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

public sealed class PlanLimitServiceTests
{
    [Fact]
    public async Task EnsureUserSlotAvailableAsync_ShouldReject_WhenTenantIsAtPlanLimit()
    {
        var tenantId = Guid.NewGuid();
        var currentSession = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Role = UserRole.Admin,
        };

        await using var dbContext = TestDbContextFactory.Create(nameof(EnsureUserSlotAvailableAsync_ShouldReject_WhenTenantIsAtPlanLimit), currentSession);
        var now = DateTimeOffset.UtcNow;

        dbContext.SubscriptionPlans.Add(new SubscriptionPlan(PlanCode.Free, "Free", "Starter", 3, 2, 5));
        dbContext.TenantSubscriptions.Add(new TenantSubscription(tenantId, PlanCode.Free, now, now.AddDays(30)));
        dbContext.Users.AddRange(
            new User(tenantId, "admin@acme.test", "Admin", "hash", UserRole.Admin),
            new User(tenantId, "manager@acme.test", "Manager", "hash", UserRole.Manager),
            new User(tenantId, "user@acme.test", "User", "hash", UserRole.User));

        await dbContext.SaveChangesAsync();

        var service = new PlanLimitService(dbContext, currentSession);

        var action = () => service.EnsureUserSlotAvailableAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<AppException>();
        exception.Which.Type.Should().Be("plan_limit_exceeded");
        exception.Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task EnsureProjectSlotAvailableAsync_ShouldAllow_WhenTenantHasCapacity()
    {
        var tenantId = Guid.NewGuid();
        var currentSession = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Role = UserRole.Manager,
        };

        await using var dbContext = TestDbContextFactory.Create(nameof(EnsureProjectSlotAvailableAsync_ShouldAllow_WhenTenantHasCapacity), currentSession);
        var now = DateTimeOffset.UtcNow;
        var owner = new User(tenantId, "manager@acme.test", "Manager", "hash", UserRole.Manager);

        dbContext.SubscriptionPlans.Add(new SubscriptionPlan(PlanCode.Pro, "Pro", "Growth", 15, 20, 40));
        dbContext.TenantSubscriptions.Add(new TenantSubscription(tenantId, PlanCode.Pro, now, now.AddDays(30)));
        dbContext.Users.Add(owner);
        dbContext.Projects.Add(new Project(tenantId, null, owner.Id, "Tenant Core", "TCORE", "Demo project", ProjectStatus.Active, null, null));

        await dbContext.SaveChangesAsync();

        var service = new PlanLimitService(dbContext, currentSession);

        var action = () => service.EnsureProjectSlotAvailableAsync(CancellationToken.None);

        await action.Should().NotThrowAsync();
    }
}

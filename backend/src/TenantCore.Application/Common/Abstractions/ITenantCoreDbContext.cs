using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TenantCore.Domain.Entities;

namespace TenantCore.Application.Common.Abstractions;

public interface ITenantCoreDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<SubscriptionPlan> SubscriptionPlans { get; }

    DbSet<TenantSubscription> TenantSubscriptions { get; }

    DbSet<TenantUsageSnapshot> TenantUsageSnapshots { get; }

    DbSet<Project> Projects { get; }

    DbSet<WorkTask> Tasks { get; }

    DbSet<Client> Clients { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

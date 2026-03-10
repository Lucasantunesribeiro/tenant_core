using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Common;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Infrastructure.Database.Seed;

public sealed class DemoDataSeeder(
    TenantCoreDbContext dbContext,
    IPasswordService passwordService,
    IClock clock,
    ILogger<DemoDataSeeder> logger)
{
    private static readonly Guid AcmeTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GlobexTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await dbContext.SubscriptionPlans.AnyAsync(cancellationToken))
        {
            await dbContext.SubscriptionPlans.AddRangeAsync(
                [
                new SubscriptionPlan(PlanCode.Free, "Free", "Starter plan for small teams.", 3, 2, 5),
                new SubscriptionPlan(PlanCode.Pro, "Pro", "Growth plan for scaling operations.", 15, 20, 40),
                new SubscriptionPlan(PlanCode.Business, "Business", "High-volume enterprise workspace.", 100, 300, 250)
                ],
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (await dbContext.Tenants.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo data already exists. Skipping seed.");
            return;
        }

        var acme = new Tenant("Acme Operations", "acme-ops", "finance@acme.test", "America/New_York");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(acme, AcmeTenantId);
        acme.UpdateSettings("Acme Operations", "finance@acme.test", "ops@acme.test", "America/New_York", "industrial", "acme.test", clock.UtcNow);

        var globex = new Tenant("Globex Advisory", "globex-advisory", "finance@globex.test", "Europe/London");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(globex, GlobexTenantId);
        globex.UpdateSettings("Globex Advisory", "finance@globex.test", "hello@globex.test", "Europe/London", "graphite", "globex.test", clock.UtcNow);

        var acmeAdmin = CreateUser(Guid.Parse("aaaa1111-1111-1111-1111-111111111111"), AcmeTenantId, "admin@acme.test", "Ava Stone", UserRole.Admin);
        var acmeManager = CreateUser(Guid.Parse("aaaa2222-2222-2222-2222-222222222222"), AcmeTenantId, "manager@acme.test", "Noah Reed", UserRole.Manager);
        var acmeUser = CreateUser(Guid.Parse("aaaa3333-3333-3333-3333-333333333333"), AcmeTenantId, "user@acme.test", "Lena Cole", UserRole.User);
        var globexAdmin = CreateUser(Guid.Parse("bbbb1111-1111-1111-1111-111111111111"), GlobexTenantId, "admin@globex.test", "Mila Brooks", UserRole.Admin);

        var acmeClient = CreateClient(Guid.Parse("c1111111-1111-1111-1111-111111111111"), AcmeTenantId, "Northwind Logistics", "contact@northwind.test", "Jordan Wells", ClientStatus.Active);
        var globexClient = CreateClient(Guid.Parse("c2222222-2222-2222-2222-222222222222"), GlobexTenantId, "Blue Orbit Retail", "ops@blueorbit.test", "Emma Ford", ClientStatus.Lead);

        var acmeProject = CreateProject(Guid.Parse("d1111111-1111-1111-1111-111111111111"), AcmeTenantId, acmeClient.Id, acmeManager.Id, "Tenant Core Rollout", "TCORE", ProjectStatus.Active);
        var globexProject = CreateProject(Guid.Parse("d2222222-2222-2222-2222-222222222222"), GlobexTenantId, globexClient.Id, globexAdmin.Id, "Advisory Portal Refresh", "GLOBEX", ProjectStatus.Planned);

        var acmeTask = CreateTask(Guid.Parse("e1111111-1111-1111-1111-111111111111"), AcmeTenantId, acmeProject.Id, acmeUser.Id, "Wire audit dashboard", WorkTaskStatus.InProgress, TaskPriority.High);
        var globexTask = CreateTask(Guid.Parse("e2222222-2222-2222-2222-222222222222"), GlobexTenantId, globexProject.Id, globexAdmin.Id, "Review workspace navigation", WorkTaskStatus.Backlog, TaskPriority.Medium);

        var acmeSubscription = new TenantSubscription(AcmeTenantId, PlanCode.Business, clock.UtcNow.AddDays(-12), clock.UtcNow.AddDays(18));
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(acmeSubscription, Guid.Parse("f1111111-1111-1111-1111-111111111111"));

        var globexSubscription = new TenantSubscription(GlobexTenantId, PlanCode.Pro, clock.UtcNow.AddDays(-7), clock.UtcNow.AddDays(23));
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(globexSubscription, Guid.Parse("f2222222-2222-2222-2222-222222222222"));

        var acmeSnapshot = new TenantUsageSnapshot(AcmeTenantId, clock.UtcNow.AddHours(-3), 3, 1, 1, 1);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(acmeSnapshot, Guid.Parse("12111111-1111-1111-1111-111111111111"));

        var globexSnapshot = new TenantUsageSnapshot(GlobexTenantId, clock.UtcNow.AddHours(-2), 1, 1, 1, 1);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(globexSnapshot, Guid.Parse("12222222-2222-2222-2222-222222222222"));

        var auditLogs = new[]
        {
            new AuditLog(AcmeTenantId, acmeAdmin.Id, "auth.login", "User", acmeAdmin.Id.ToString(), "seed-acme", "{\"seed\":true}", clock.UtcNow.AddHours(-2)),
            new AuditLog(AcmeTenantId, acmeAdmin.Id, "project.created", "Project", acmeProject.Id.ToString(), "seed-acme", "{\"seed\":true}", clock.UtcNow.AddHours(-1)),
            new AuditLog(GlobexTenantId, globexAdmin.Id, "subscription.plan_changed", "TenantSubscription", globexSubscription.Id.ToString(), "seed-globex", "{\"seed\":true}", clock.UtcNow.AddMinutes(-45))
        };

        foreach (var log in auditLogs)
        {
            typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(log, Guid.NewGuid());
        }

        await dbContext.Tenants.AddRangeAsync([acme, globex], cancellationToken);
        await dbContext.Users.AddRangeAsync([acmeAdmin, acmeManager, acmeUser, globexAdmin], cancellationToken);
        await dbContext.Clients.AddRangeAsync([acmeClient, globexClient], cancellationToken);
        await dbContext.Projects.AddRangeAsync([acmeProject, globexProject], cancellationToken);
        await dbContext.Tasks.AddRangeAsync([acmeTask, globexTask], cancellationToken);
        await dbContext.TenantSubscriptions.AddRangeAsync([acmeSubscription, globexSubscription], cancellationToken);
        await dbContext.TenantUsageSnapshots.AddRangeAsync([acmeSnapshot, globexSnapshot], cancellationToken);
        await dbContext.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded demo data for tenant_core. Demo password: Passw0rd!");
    }

    private User CreateUser(Guid id, Guid tenantId, string email, string fullName, UserRole role)
    {
        var user = new User(tenantId, email, fullName, passwordService.Hash("Passw0rd!"), role);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(user, id);
        user.MarkLogin(clock.UtcNow.AddMinutes(-30));
        return user;
    }

    private static Client CreateClient(Guid id, Guid tenantId, string name, string email, string contactName, ClientStatus status)
    {
        var client = new Client(tenantId, name, email, contactName, status, "Priority account");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(client, id);
        return client;
    }

    private static Project CreateProject(Guid id, Guid tenantId, Guid clientId, Guid ownerUserId, string name, string code, ProjectStatus status)
    {
        var project = new Project(tenantId, clientId, ownerUserId, name, code, "Seeded demo project", status, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(project, id);
        return project;
    }

    private static WorkTask CreateTask(Guid id, Guid tenantId, Guid projectId, Guid assigneeUserId, string title, WorkTaskStatus status, TaskPriority priority)
    {
        var task = new WorkTask(tenantId, projectId, assigneeUserId, title, "Seeded demo task", status, priority, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(task, id);
        return task;
    }
}

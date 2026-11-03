using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Clock;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Database.Repositories;
using TenantCore.Infrastructure.Jobs;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests.Database;

/// <summary>
/// Suíte de isolamento contra banco real, executada uma vez por provider
/// (<c>Category=SqlServer</c> e <c>Category=Postgres</c>).
/// </summary>
public abstract class TenantIsolationDatabaseTests<TFixture>(TFixture fixture)
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task DapperDashboard_ShouldOnlyReturnRowsOfRequestedTenant()
    {
        var a = await fixture.SeedTenantAsync("alpha", taskCount: 2);
        var b = await fixture.SeedTenantAsync("bravo", taskCount: 3);

        // Linha "envenenada" (o que o bug de UpdateTask permitia gravar): tarefa do tenant B
        // apontando para o projeto do tenant A. O SQL do dashboard não pode contá-la para A.
        await using (var maintenance = fixture.CreateDbContext(tenantId: null))
        {
            maintenance.Tasks.Add(new WorkTask(b.TenantId, a.ProjectId, b.OwnerId, "poisoned", string.Empty, WorkTaskStatus.Blocked, TaskPriority.High, null));
            await maintenance.SaveChangesAsync();
        }

        var repository = new TenantDashboardRepository(fixture.Configuration);

        var dashboardA = await repository.GetAsync(a.TenantId);
        dashboardA.TenantId.Should().Be(a.TenantId);
        var rowA = dashboardA.Projects.Should().ContainSingle().Subject;
        rowA.ProjectId.Should().Be(a.ProjectId);
        rowA.TotalTasks.Should().Be(2);
        rowA.BlockedTasks.Should().Be(0);
        rowA.ClientName.Should().Be(a.ClientName);
        rowA.OwnerFullName.Should().Be(a.OwnerName);

        var dashboardB = await repository.GetAsync(b.TenantId);
        var rowB = dashboardB.Projects.Should().ContainSingle().Subject;
        rowB.ProjectId.Should().Be(b.ProjectId);
        rowB.TotalTasks.Should().Be(3);
    }

    [Fact]
    public async Task EfQueryFilters_ShouldTranslateToTenantPredicate()
    {
        var a = await fixture.SeedTenantAsync("charlie", taskCount: 1);
        var b = await fixture.SeedTenantAsync("delta", taskCount: 4);

        await using var contextA = fixture.CreateDbContext(a.TenantId);
        (await contextA.Projects.Select(x => x.Id).ToListAsync()).Should().Equal(a.ProjectId);
        (await contextA.Tasks.CountAsync()).Should().Be(1);
        (await contextA.Clients.AnyAsync(x => x.Id == b.ClientId)).Should().BeFalse();
        (await contextA.Users.SingleOrDefaultAsync(x => x.Id == b.OwnerId)).Should().BeNull();

        await using var noTenant = fixture.CreateDbContext(tenantId: null);
        (await noTenant.Projects.AnyAsync()).Should().BeFalse("sem tenant os filtros devem ser fail-closed");
    }

    [Fact]
    public async Task WriteGuard_ShouldBlockCrossTenantUpdateAndDelete()
    {
        var a = await fixture.SeedTenantAsync("echo");
        var b = await fixture.SeedTenantAsync("foxtrot");

        await using (var contextA = fixture.CreateDbContext(a.TenantId))
        {
            var foreignClient = await contextA.Clients.IgnoreQueryFilters().SingleAsync(x => x.Id == b.ClientId);
            foreignClient.Update("Hijacked", "x@x.test", "x", ClientStatus.Inactive, "x", DateTimeOffset.UtcNow);

            var update = () => contextA.SaveChangesAsync();
            await update.Should().ThrowAsync<TenantIsolationViolationException>();
        }

        await using (var contextA = fixture.CreateDbContext(a.TenantId))
        {
            var foreignProject = await contextA.Projects.IgnoreQueryFilters().SingleAsync(x => x.Id == b.ProjectId);
            contextA.Projects.Remove(foreignProject);

            var delete = () => contextA.SaveChangesAsync();
            await delete.Should().ThrowAsync<TenantIsolationViolationException>();
        }

        await using var contextB = fixture.CreateDbContext(b.TenantId);
        (await contextB.Clients.SingleAsync(x => x.Id == b.ClientId)).Name.Should().Be(b.ClientName);
        (await contextB.Projects.AnyAsync(x => x.Id == b.ProjectId)).Should().BeTrue();
    }

    [Fact]
    public async Task UniqueIndexes_ShouldBeScopedPerTenant()
    {
        // Os dois tenants semeados usam o código de projeto "SHARED": permitido entre tenants.
        var a = await fixture.SeedTenantAsync("golf");
        await fixture.SeedTenantAsync("hotel");

        await using var contextA = fixture.CreateDbContext(a.TenantId);
        contextA.Projects.Add(new Project(a.TenantId, null, null, "Duplicate", "SHARED", string.Empty, ProjectStatus.Planned, null, null));

        var act = () => contextA.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        fixture.IsUniqueViolation(thrown.Which).Should().BeTrue("o índice único (TenantId, Code) deve barrar o duplicado");
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldRaiseConcurrencyConflict()
    {
        // SQL Server: rowversion; PostgreSQL: coluna de sistema xmin.
        var a = await fixture.SeedTenantAsync("kilo");

        await using var first = fixture.CreateDbContext(a.TenantId);
        await using var second = fixture.CreateDbContext(a.TenantId);
        var clientFirst = await first.Clients.SingleAsync(x => x.Id == a.ClientId);
        var clientSecond = await second.Clients.SingleAsync(x => x.Id == a.ClientId);

        clientFirst.Update("First writer", "first@kilo.test", "First", ClientStatus.Active, string.Empty, DateTimeOffset.UtcNow);
        await first.SaveChangesAsync();

        clientSecond.Update("Second writer", "second@kilo.test", "Second", ClientStatus.Active, string.Empty, DateTimeOffset.UtcNow);
        var act = () => second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var reader = fixture.CreateDbContext(a.TenantId);
        (await reader.Clients.SingleAsync(x => x.Id == a.ClientId)).Name.Should().Be("First writer");
    }

    [Fact]
    public async Task UsageSnapshotJob_ShouldCountEachTenantSeparatelyAndInvalidateTenantKeys()
    {
        var a = await fixture.SeedTenantAsync("india", taskCount: 1);
        var b = await fixture.SeedTenantAsync("juliet", taskCount: 5);
        var cache = new TestCacheService();
        await cache.SetAsync($"usage:{a.TenantId}", "stale-a", TimeSpan.FromMinutes(5));
        await cache.SetAsync($"usage:{b.TenantId}", "stale-b", TimeSpan.FromMinutes(5));

        await using (var jobContext = fixture.CreateDbContext(tenantId: null))
        {
            var job = new UsageSnapshotJob(jobContext, new SystemClock(), cache, NullLogger<UsageSnapshotJob>.Instance);
            var executionContext = Substitute.For<IJobExecutionContext>();
            executionContext.CancellationToken.Returns(CancellationToken.None);

            await job.Execute(executionContext);
        }

        await using var contextA = fixture.CreateDbContext(a.TenantId);
        var snapshotA = await contextA.TenantUsageSnapshots.SingleAsync();
        snapshotA.TasksCount.Should().Be(1);
        snapshotA.ProjectsCount.Should().Be(1);
        snapshotA.ActiveUsers.Should().Be(1);

        await using var contextB = fixture.CreateDbContext(b.TenantId);
        (await contextB.TenantUsageSnapshots.SingleAsync()).TasksCount.Should().Be(5);

        (await cache.GetAsync<string>($"usage:{a.TenantId}")).Should().BeNull();
        (await cache.GetAsync<string>($"usage:{b.TenantId}")).Should().BeNull();
    }
}

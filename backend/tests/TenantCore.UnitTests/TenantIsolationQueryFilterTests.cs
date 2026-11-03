using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenantCore.Domain.Common;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

public sealed class TenantIsolationQueryFilterTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static TestCurrentSession SessionFor(Guid tenantId) => new()
    {
        UserId = Guid.NewGuid(),
        TenantId = tenantId,
        Role = UserRole.Admin,
    };

    /// <summary>
    /// Semeia dados dos dois tenants por um contexto sem tenant (mesmo caminho do seed de startup),
    /// pois a barreira de escrita impede um contexto do tenant A de gravar linhas do tenant B.
    /// </summary>
    private static async Task SeedBothTenantsAsync(string databaseName)
    {
        await using var seedContext = TestDbContextFactory.Create(databaseName, new TestCurrentSession());
        var now = DateTimeOffset.UtcNow;

        foreach (var (tenantId, label) in new[] { (TenantA, "a"), (TenantB, "b") })
        {
            var user = new User(tenantId, $"admin@{label}.test", $"Admin {label}", "hash", UserRole.Admin);
            var client = new Client(tenantId, $"Client {label}", $"ops@{label}.test", "Contact", ClientStatus.Active, label);
            var project = new Project(tenantId, client.Id, user.Id, $"Project {label}", $"P{label}".ToUpperInvariant(), label, ProjectStatus.Active, null, null);

            seedContext.AddRange(
                user,
                client,
                project,
                new WorkTask(tenantId, project.Id, user.Id, $"Task {label}", label, WorkTaskStatus.Backlog, TaskPriority.Low, null),
                new RefreshToken(tenantId, user.Id, $"hash-{label}-{Guid.NewGuid():N}", now.AddDays(1), "", ""),
                new TenantSubscription(tenantId, PlanCode.Free, now, now.AddMonths(1)),
                new TenantUsageSnapshot(tenantId, now, 1, 1, 1, 1),
                new AuditLog(tenantId, user.Id, "seed", "Test", label, "corr", "{}", now));
        }

        await seedContext.SaveChangesAsync();
    }

    [Fact]
    public async Task QueryFilters_ShouldOnlyExposeCurrentTenantRows()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedBothTenantsAsync(databaseName);

        await using var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA));

        var clients = await dbContext.Clients.ToListAsync();

        clients.Should().ContainSingle();
        clients[0].TenantId.Should().Be(TenantA);
        clients[0].Name.Should().Be("Client a");
    }

    [Fact]
    public async Task QueryFilters_ShouldIsolateEveryTenantOwnedDbSet()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedBothTenantsAsync(databaseName);

        await using var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantB));

        (await dbContext.Users.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.Clients.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.Projects.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.Tasks.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.RefreshTokens.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.TenantSubscriptions.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.TenantUsageSnapshots.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
        (await dbContext.AuditLogs.Select(x => x.TenantId).ToListAsync()).Should().OnlyContain(id => id == TenantB).And.NotBeEmpty();
    }

    [Fact]
    public async Task QueryFilters_WithoutTenantInSession_ShouldFailClosed()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedBothTenantsAsync(databaseName);

        await using var dbContext = TestDbContextFactory.Create(databaseName, new TestCurrentSession());

        (await dbContext.Users.AnyAsync()).Should().BeFalse();
        (await dbContext.Projects.AnyAsync()).Should().BeFalse();
        (await dbContext.AuditLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task FilteredLookupById_ForAnotherTenantRow_ShouldReturnNull()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedBothTenantsAsync(databaseName);

        Guid tenantBProjectId;
        await using (var tenantBContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantB)))
        {
            tenantBProjectId = await tenantBContext.Projects.Select(x => x.Id).SingleAsync();
        }

        await using var tenantAContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA));

        (await tenantAContext.Projects.SingleOrDefaultAsync(x => x.Id == tenantBProjectId)).Should().BeNull();
    }

    [Fact]
    public void Model_EveryTenantOwnedEntity_ShouldHaveGlobalQueryFilter()
    {
        using var dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString("N"), SessionFor(TenantA));

        var tenantOwnedTypes = dbContext.Model.GetEntityTypes()
            .Where(type => typeof(ITenantOwnedEntity).IsAssignableFrom(type.ClrType))
            .ToList();

        tenantOwnedTypes.Should().NotBeEmpty();
        tenantOwnedTypes.Should().OnlyContain(
            type => type.GetQueryFilter() != null,
            "toda entidade tenant-owned precisa de HasQueryFilter por TenantId");
    }

    [Fact]
    public void Domain_EveryTenantOwnedEntity_ShouldBeMappedInDbContext()
    {
        using var dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString("N"), SessionFor(TenantA));

        var mapped = dbContext.Model.GetEntityTypes().Select(type => type.ClrType).ToHashSet();
        var declared = typeof(TenantOwnedEntity).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITenantOwnedEntity).IsAssignableFrom(type));

        declared.Should().OnlyContain(type => mapped.Contains(type),
            "uma entidade tenant-owned fora do modelo EF não teria query filter");
    }
}

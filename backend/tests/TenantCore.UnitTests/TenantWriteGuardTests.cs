using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

/// <summary>
/// Barreira de escrita do TenantCoreDbContext.SaveChanges: query filters protegem leitura,
/// estes testes provam que a escrita também não cruza a fronteira de tenant.
/// </summary>
public sealed class TenantWriteGuardTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static TestCurrentSession SessionFor(Guid tenantId) => new()
    {
        UserId = Guid.NewGuid(),
        TenantId = tenantId,
        Role = UserRole.Admin,
    };

    private static Client NewClient(Guid tenantId, string name = "Client") =>
        new(tenantId, name, "ops@client.test", "Contact", ClientStatus.Active, string.Empty);

    private static async Task<Guid> SeedClientAsync(string databaseName, Guid tenantId)
    {
        await using var seedContext = TestDbContextFactory.Create(databaseName, new TestCurrentSession());
        var client = NewClient(tenantId, $"Seed {tenantId:N}");
        seedContext.Clients.Add(client);
        await seedContext.SaveChangesAsync();
        return client.Id;
    }

    [Fact]
    public async Task Add_WithEmptyTenantId_ShouldStampSessionTenant()
    {
        await using var dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString("N"), SessionFor(TenantA));
        var client = NewClient(Guid.Empty);

        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        client.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task Add_ForAnotherTenant_ShouldThrowAndPersistNothing()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA));

        dbContext.Clients.Add(NewClient(TenantB));
        var act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantIsolationViolationException>();

        await using var tenantBContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantB));
        (await tenantBContext.Clients.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public void SyncSaveChanges_ForAnotherTenant_ShouldAlsoThrow()
    {
        using var dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString("N"), SessionFor(TenantA));

        dbContext.Clients.Add(NewClient(TenantB));
        var act = () => dbContext.SaveChanges();

        act.Should().Throw<TenantIsolationViolationException>();
    }

    [Fact]
    public async Task Modify_TenantIdOfExistingEntity_ShouldThrow()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedClientAsync(databaseName, TenantA);

        await using var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA));
        var client = await dbContext.Clients.SingleAsync();

        dbContext.Entry(client).Property(x => x.TenantId).CurrentValue = TenantB;
        var act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantIsolationViolationException>().WithMessage("*immutable*");
    }

    [Fact]
    public async Task Modify_TenantIdWithoutSession_ShouldStillThrow()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedClientAsync(databaseName, TenantA);

        await using var jobContext = TestDbContextFactory.Create(databaseName, new TestCurrentSession());
        var client = await jobContext.Clients.IgnoreQueryFilters().SingleAsync();

        jobContext.Entry(client).Property(x => x.TenantId).CurrentValue = TenantB;
        var act = () => jobContext.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantIsolationViolationException>();
    }

    [Fact]
    public async Task Modify_RowOfAnotherTenant_ShouldThrow()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedClientAsync(databaseName, TenantB);

        await using var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA));
        // Simula um bypass indevido (IgnoreQueryFilters) num handler de request.
        var foreignClient = await dbContext.Clients.IgnoreQueryFilters().SingleAsync();

        foreignClient.Update("Hijacked", "x@x.test", "x", ClientStatus.Inactive, "x", DateTimeOffset.UtcNow);
        var act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantIsolationViolationException>();
    }

    [Fact]
    public async Task Delete_RowOfAnotherTenant_ShouldThrowAndKeepRow()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedClientAsync(databaseName, TenantB);

        await using (var dbContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantA)))
        {
            var foreignClient = await dbContext.Clients.IgnoreQueryFilters().SingleAsync();
            dbContext.Clients.Remove(foreignClient);
            var act = () => dbContext.SaveChangesAsync();
            await act.Should().ThrowAsync<TenantIsolationViolationException>();
        }

        await using var tenantBContext = TestDbContextFactory.Create(databaseName, SessionFor(TenantB));
        (await tenantBContext.Clients.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task WithoutSessionTenant_CrossTenantMaintenance_ShouldBeAllowed()
    {
        // Jobs Quartz (sem HttpContext) operam entre tenants de propósito.
        var databaseName = Guid.NewGuid().ToString("N");
        await SeedClientAsync(databaseName, TenantA);
        await SeedClientAsync(databaseName, TenantB);

        await using var jobContext = TestDbContextFactory.Create(databaseName, new TestCurrentSession());
        var clients = await jobContext.Clients.IgnoreQueryFilters().ToListAsync();
        clients[0].Update("Renamed", "x@x.test", "x", ClientStatus.Active, "", DateTimeOffset.UtcNow);
        jobContext.Clients.Remove(clients[1]);

        var act = () => jobContext.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WithoutSessionTenant_AddWithEmptyTenantId_ShouldThrow()
    {
        await using var jobContext = TestDbContextFactory.Create(Guid.NewGuid().ToString("N"), new TestCurrentSession());

        jobContext.Clients.Add(NewClient(Guid.Empty));
        var act = () => jobContext.SaveChangesAsync();

        await act.Should().ThrowAsync<TenantIsolationViolationException>();
    }
}

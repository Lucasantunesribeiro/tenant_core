using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

public sealed class TenantIsolationQueryFilterTests
{
    [Fact]
    public async Task QueryFilters_ShouldOnlyExposeCurrentTenantRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentSession = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantA,
            Role = UserRole.Admin,
        };

        await using var dbContext = TestDbContextFactory.Create(nameof(QueryFilters_ShouldOnlyExposeCurrentTenantRows), currentSession);

        dbContext.Clients.AddRange(
            new Client(tenantA, "Acme Client", "ops@acme.test", "Jordan Wells", ClientStatus.Active, "tenant-a"),
            new Client(tenantB, "Globex Client", "ops@globex.test", "Emma Ford", ClientStatus.Lead, "tenant-b"));

        await dbContext.SaveChangesAsync();

        var clients = await dbContext.Clients.ToListAsync();

        clients.Should().HaveCount(1);
        clients[0].TenantId.Should().Be(tenantA);
        clients[0].Name.Should().Be("Acme Client");
    }
}

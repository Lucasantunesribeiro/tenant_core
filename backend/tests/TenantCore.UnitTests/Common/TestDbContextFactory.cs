using Microsoft.EntityFrameworkCore;
using TenantCore.Infrastructure.Database;

namespace TenantCore.UnitTests.Common;

internal static class TestDbContextFactory
{
    public static TenantCoreDbContext Create(string databaseName, TestCurrentSession currentSession)
    {
        var options = new DbContextOptionsBuilder<TenantCoreDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TenantCoreDbContext(options, currentSession);
    }
}

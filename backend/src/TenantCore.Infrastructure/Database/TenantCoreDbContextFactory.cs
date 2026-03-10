using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Enums;

namespace TenantCore.Infrastructure.Database;

public sealed class TenantCoreDbContextFactory : IDesignTimeDbContextFactory<TenantCoreDbContext>
{
    public TenantCoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantCoreDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("TENANT_CORE_CONNSTRING") ??
            "Host=localhost;Port=5432;Database=tenant_core;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, pg =>
        {
            pg.MigrationsAssembly(typeof(TenantCoreDbContext).Assembly.FullName);
        });

        return new TenantCoreDbContext(optionsBuilder.Options, new DesignTimeCurrentSession());
    }

    private sealed class DesignTimeCurrentSession : ICurrentSession
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public string? Email => null;
        public UserRole? Role => null;
        public string CorrelationId => "design-time";
        public bool IsAuthenticated => false;
        public void EnsureAuthenticated() => throw new InvalidOperationException();
        public void EnsureTenantAvailable() => throw new InvalidOperationException();
    }
}

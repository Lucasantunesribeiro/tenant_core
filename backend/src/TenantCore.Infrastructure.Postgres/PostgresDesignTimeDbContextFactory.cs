using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;

namespace TenantCore.Infrastructure.Postgres;

/// <summary>
/// Factory de design-time para gerar as migrations de PostgreSQL:
/// <code>dotnet ef migrations add Nome --project src/TenantCore.Infrastructure.Postgres --startup-project src/TenantCore.Infrastructure.Postgres</code>
/// </summary>
public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TenantCoreDbContext>
{
    public TenantCoreDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TENANT_CORE_PG_CONNSTRING") ??
            "Host=localhost;Port=5432;Database=tenant_core;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<TenantCoreDbContext>();
        builder.UseConfiguredDatabase(DatabaseProvider.PostgreSQL, DatabaseProviderSettings.NormalizePostgresConnectionString(connectionString));

        return new TenantCoreDbContext(builder.Options, new DesignTimeCurrentSession());
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

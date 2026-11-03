using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantCore.Infrastructure.Database;
using TenantCore.IntegrationTests.Database;
using Testcontainers.PostgreSql;

namespace TenantCore.IntegrationTests.Postgres;

/// <summary>
/// PostgreSQL 16 real (Testcontainers) com as migrations de TenantCore.Infrastructure.Postgres aplicadas.
/// Roda a mesma suíte de isolamento do SQL Server para garantir paridade do modo demo.
/// </summary>
public sealed class PostgresFixture : DatabaseFixture
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public override DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

    protected override async Task<string> StartDatabaseAsync()
    {
        await _container.StartAsync();
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "tenant_core_tests",
        }.ConnectionString;
    }

    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public override bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class PostgresTenantIsolationTests(PostgresFixture fixture)
    : TenantIsolationDatabaseTests<PostgresFixture>(fixture);

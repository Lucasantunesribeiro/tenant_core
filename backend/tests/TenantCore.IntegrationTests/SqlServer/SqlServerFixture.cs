using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TenantCore.Infrastructure.Database;
using TenantCore.IntegrationTests.Database;
using Testcontainers.MsSql;

namespace TenantCore.IntegrationTests.SqlServer;

/// <summary>SQL Server 2022 real (Testcontainers) com as migrations de SQL Server aplicadas.</summary>
public sealed class SqlServerFixture : DatabaseFixture
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

    protected override async Task<string> StartDatabaseAsync()
    {
        await _container.StartAsync();
        return new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "tenant_core_tests",
        }.ConnectionString;
    }

    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // 2601 = índice único; 2627 = constraint unique/PK.
    public override bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerTenantIsolationTests(SqlServerFixture fixture)
    : TenantIsolationDatabaseTests<SqlServerFixture>(fixture);

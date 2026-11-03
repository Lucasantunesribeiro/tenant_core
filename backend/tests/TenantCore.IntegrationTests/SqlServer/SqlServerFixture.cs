using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;
using Testcontainers.MsSql;

namespace TenantCore.IntegrationTests.SqlServer;

/// <summary>
/// SQL Server real (Testcontainers) com as migrations do projeto aplicadas.
/// Necessário para o que o provider InMemory não cobre: SQL bruto do Dapper
/// (que não passa pelos query filters do EF), tradução dos filtros para T-SQL,
/// índices únicos compostos por tenant e o job cross-tenant.
/// Requer Docker; o CI (ubuntu-latest) já tem.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public IConfiguration Configuration { get; private set; } = new ConfigurationBuilder().Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "tenant_core_tests",
        }.ConnectionString;

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:SqlServer"] = ConnectionString })
            .Build();

        DapperConfig.RegisterTypeHandlers();

        await using var dbContext = CreateDbContext(tenantId: null);
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>tenantId nulo = contexto sem tenant (mesmo cenário dos jobs Quartz e do seed).</summary>
    public TenantCoreDbContext CreateDbContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<TenantCoreDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new TenantCoreDbContext(options, new FixedSession(tenantId));
    }

    public async Task<SeededTenant> SeedTenantAsync(string label, int taskCount = 2)
    {
        await using var dbContext = CreateDbContext(tenantId: null);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var tenant = new Tenant($"Tenant {label}", $"{label}-{suffix}", $"billing@{label}.test", "UTC");
        dbContext.Tenants.Add(tenant);

        var owner = new User(tenant.Id, $"owner-{suffix}@{label}.test", $"Owner {label}", "hash", UserRole.Manager);
        var client = new Client(tenant.Id, $"Client {label}", $"client@{label}.test", "Contact", ClientStatus.Active, string.Empty);
        var project = new Project(tenant.Id, client.Id, owner.Id, $"Project {label}", "SHARED", string.Empty, ProjectStatus.Active, null, null);
        dbContext.AddRange(owner, client, project);

        for (var i = 0; i < taskCount; i++)
        {
            dbContext.Tasks.Add(new WorkTask(tenant.Id, project.Id, owner.Id, $"Task {label} {i}", string.Empty, WorkTaskStatus.Backlog, TaskPriority.Low, null));
        }

        await dbContext.SaveChangesAsync();
        return new SeededTenant(tenant.Id, owner.Id, client.Id, project.Id, $"Project {label}", $"Client {label}", $"Owner {label}");
    }

    private sealed class FixedSession(Guid? tenantId) : ICurrentSession
    {
        public Guid? UserId => tenantId is null ? null : Guid.Empty;

        public Guid? TenantId => tenantId;

        public string? Email => null;

        public UserRole? Role => UserRole.Admin;

        public string CorrelationId => "sqlserver-tests";

        public bool IsAuthenticated => tenantId is not null;

        public void EnsureAuthenticated()
        {
        }

        public void EnsureTenantAvailable()
        {
            if (tenantId is null)
            {
                throw new AppException("tenant_header_required", "Tenant required", 400, "Tenant required.");
            }
        }
    }
}

public sealed record SeededTenant(
    Guid TenantId,
    Guid OwnerId,
    Guid ClientId,
    Guid ProjectId,
    string ProjectName,
    string ClientName,
    string OwnerName);

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

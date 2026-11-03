using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;

namespace TenantCore.IntegrationTests.Database;

/// <summary>
/// Banco relacional real (Testcontainers) com as migrations do provider aplicadas.
/// Necessário para o que o provider InMemory não cobre: SQL bruto do Dapper
/// (que não passa pelos query filters do EF), tradução dos filtros para SQL,
/// índices únicos compostos por tenant, token de concorrência e o job cross-tenant.
/// Requer Docker; o CI (ubuntu-latest) já tem.
/// </summary>
public abstract class DatabaseFixture : IAsyncLifetime
{
    public abstract DatabaseProvider Provider { get; }

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Configuração mínima lida por <see cref="DatabaseProviderSettings"/> (provider + connection string).</summary>
    public IConfiguration Configuration { get; private set; } = new ConfigurationBuilder().Build();

    public async Task InitializeAsync()
    {
        ConnectionString = await StartDatabaseAsync();

        var connectionName = Provider == DatabaseProvider.PostgreSQL
            ? DatabaseProviderSettings.PostgresConnectionName
            : DatabaseProviderSettings.SqlServerConnectionName;

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseProviderSettings.ProviderKey] = Provider.ToString(),
                [$"ConnectionStrings:{connectionName}"] = ConnectionString,
            })
            .Build();

        DapperConfig.RegisterTypeHandlers();

        await using var dbContext = CreateDbContext(tenantId: null);
        await dbContext.Database.MigrateAsync();
    }

    public abstract Task DisposeAsync();

    /// <summary>Sobe o banco e devolve a connection string de um database dedicado aos testes.</summary>
    protected abstract Task<string> StartDatabaseAsync();

    /// <summary>Confere que a falha veio de violação de índice único no provider.</summary>
    public abstract bool IsUniqueViolation(DbUpdateException exception);

    /// <summary>tenantId nulo = contexto sem tenant (mesmo cenário dos jobs Quartz e do seed).</summary>
    public TenantCoreDbContext CreateDbContext(Guid? tenantId)
    {
        var builder = new DbContextOptionsBuilder<TenantCoreDbContext>();
        builder.UseConfiguredDatabase(Provider, ConnectionString);
        return new TenantCoreDbContext(builder.Options, new FixedSession(tenantId));
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

        public string CorrelationId => "database-tests";

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

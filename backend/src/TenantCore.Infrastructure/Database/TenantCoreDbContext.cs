using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Common;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Infrastructure.Database;

public sealed class TenantCoreDbContext(
    DbContextOptions<TenantCoreDbContext> options,
    ICurrentSession currentSession) : DbContext(options), ITenantCoreDbContext
{
    private Guid CurrentTenantId => currentSession.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    public DbSet<TenantUsageSnapshot> TenantUsageSnapshots => Set<TenantUsageSnapshot>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Barreira de escrita multi-tenant (complementa os query filters, que só protegem leitura):
    /// - Added: TenantId vazio é carimbado com o tenant da sessão; TenantId de outro tenant é rejeitado.
    /// - Modified: TenantId de entidade existente é imutável; só se altera linha do tenant da sessão.
    /// - Deleted: só se apaga linha do tenant da sessão.
    /// Sem tenant na sessão (jobs Quartz, seed de startup) as checagens de "mesmo tenant" não se
    /// aplicam, pois esses fluxos operam entre tenants de propósito; a imutabilidade do TenantId
    /// e a proibição de TenantId vazio continuam valendo.
    /// </summary>
    private void EnforceTenantOwnership()
    {
        var sessionTenantId = currentSession.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantOwnedEntity>())
        {
            var tenantProperty = entry.Property(nameof(ITenantOwnedEntity.TenantId));
            var entityName = entry.Metadata.ClrType.Name;

            switch (entry.State)
            {
                case EntityState.Added:
                {
                    var tenantId = (Guid)tenantProperty.CurrentValue!;
                    if (tenantId == Guid.Empty)
                    {
                        if (sessionTenantId is null)
                        {
                            throw new TenantIsolationViolationException(
                                $"{entityName} cannot be created without a TenantId outside a tenant context.");
                        }

                        tenantProperty.CurrentValue = sessionTenantId.Value;
                    }
                    else if (sessionTenantId is not null && tenantId != sessionTenantId.Value)
                    {
                        throw new TenantIsolationViolationException(
                            $"{entityName} cannot be created for a tenant other than the current session tenant.");
                    }

                    break;
                }

                case EntityState.Modified:
                {
                    var originalTenantId = (Guid)tenantProperty.OriginalValue!;
                    var currentTenantId = (Guid)tenantProperty.CurrentValue!;
                    if (tenantProperty.IsModified || originalTenantId != currentTenantId)
                    {
                        throw new TenantIsolationViolationException($"{entityName}.TenantId is immutable.");
                    }

                    if (sessionTenantId is not null && originalTenantId != sessionTenantId.Value)
                    {
                        throw new TenantIsolationViolationException(
                            $"{entityName} belongs to another tenant and cannot be modified.");
                    }

                    break;
                }

                case EntityState.Deleted:
                {
                    var originalTenantId = (Guid)tenantProperty.OriginalValue!;
                    if (sessionTenantId is not null && originalTenantId != sessionTenantId.Value)
                    {
                        throw new TenantIsolationViolationException(
                            $"{entityName} belongs to another tenant and cannot be deleted.");
                    }

                    break;
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTenant(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureBilling(modelBuilder);
        ConfigureWorkspace(modelBuilder);
        ConfigureAudit(modelBuilder);
    }

    private void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(80);
            entity.Property(x => x.BillingEmail).HasMaxLength(180);
            entity.Property(x => x.SupportEmail).HasMaxLength(180);
            entity.Property(x => x.TimeZone).HasMaxLength(80);
            entity.Property(x => x.Theme).HasMaxLength(40);
            entity.Property(x => x.AllowedDomains).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Slug).IsUnique();
        });
    }

    private void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.FullName).HasMaxLength(120);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Role });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(x => x.CreatedByIp).HasMaxLength(120);
            entity.Property(x => x.UserAgent).HasMaxLength(300);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.ExpiresAtUtc });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureBilling(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Name).HasMaxLength(60);
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("TenantSubscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanCode).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.QuotaState).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.WarningMessage).HasMaxLength(240);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TenantUsageSnapshot>(entity =>
        {
            entity.ToTable("TenantUsageSnapshots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.CapturedAtUtc });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureWorkspace(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.ContactName).HasMaxLength(120);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkTask>(entity =>
        {
            entity.ToTable("Tasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.AssigneeUserId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.CorrelationId).HasMaxLength(120);
            entity.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.TenantId, x.Action, x.EntityType });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId);
        });
    }
}

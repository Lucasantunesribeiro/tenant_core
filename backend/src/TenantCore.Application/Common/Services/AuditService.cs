using System.Text.Json;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Entities;

namespace TenantCore.Application.Common.Services;

public sealed class AuditService(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock) : IAuditService
{
    public async Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object metadata,
        CancellationToken cancellationToken)
    {
        var tenantId = currentSession.GetRequiredTenantId();

        await dbContext.AuditLogs.AddAsync(
            new AuditLog(
                tenantId,
                currentSession.UserId,
                action,
                entityType,
                entityId,
                currentSession.CorrelationId,
                JsonSerializer.Serialize(metadata),
                clock.UtcNow),
            cancellationToken);
    }
}

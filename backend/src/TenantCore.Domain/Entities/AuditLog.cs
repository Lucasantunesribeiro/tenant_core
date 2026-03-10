using TenantCore.Domain.Common;

namespace TenantCore.Domain.Entities;

public sealed class AuditLog : TenantOwnedEntity
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid tenantId,
        Guid? actorUserId,
        string action,
        string entityType,
        string entityId,
        string correlationId,
        string metadataJson,
        DateTimeOffset occurredAtUtc)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = "{}";

    public DateTimeOffset OccurredAtUtc { get; private set; }
}

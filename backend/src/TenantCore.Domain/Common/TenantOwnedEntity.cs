namespace TenantCore.Domain.Common;

public abstract class TenantOwnedEntity : AuditableEntity, ITenantOwnedEntity
{
    public Guid TenantId { get; protected set; }
}

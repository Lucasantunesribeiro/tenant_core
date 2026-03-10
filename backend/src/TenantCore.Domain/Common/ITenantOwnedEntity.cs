namespace TenantCore.Domain.Common;

public interface ITenantOwnedEntity
{
    Guid TenantId { get; }
}

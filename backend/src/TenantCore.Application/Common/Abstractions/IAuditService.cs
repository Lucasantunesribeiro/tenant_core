namespace TenantCore.Application.Common.Abstractions;

public interface IAuditService
{
    Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object metadata,
        CancellationToken cancellationToken);
}

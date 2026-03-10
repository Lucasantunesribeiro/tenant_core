using TenantCore.Domain.Enums;

namespace TenantCore.Application.Common.Abstractions;

public interface ICurrentSession
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    string? Email { get; }

    UserRole? Role { get; }

    string CorrelationId { get; }

    bool IsAuthenticated { get; }

    void EnsureAuthenticated();

    void EnsureTenantAvailable();
}

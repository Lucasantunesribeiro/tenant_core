using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.UnitTests.Common;

internal sealed class TestCurrentSession : ICurrentSession
{
    public Guid? UserId { get; init; }

    public Guid? TenantId { get; init; }

    public string? Email { get; init; }

    public UserRole? Role { get; init; }

    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    public bool IsAuthenticated => UserId.HasValue;

    public void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new AppException("unauthorized", "Unauthorized", 401, "Authentication is required for this operation.");
        }
    }

    public void EnsureTenantAvailable()
    {
        if (!TenantId.HasValue)
        {
            throw new AppException("tenant_header_required", "Tenant required", 400, "The X-Tenant-Id header is required.");
        }
    }
}

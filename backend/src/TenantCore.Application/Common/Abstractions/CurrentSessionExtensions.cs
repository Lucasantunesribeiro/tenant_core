using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Common.Abstractions;

public static class CurrentSessionExtensions
{
    public static Guid GetRequiredTenantId(this ICurrentSession currentSession)
    {
        currentSession.EnsureTenantAvailable();
        return currentSession.TenantId!.Value;
    }

    public static Guid GetRequiredUserId(this ICurrentSession currentSession)
    {
        currentSession.EnsureAuthenticated();
        return currentSession.UserId!.Value;
    }

    public static void EnsureAdmin(this ICurrentSession currentSession)
    {
        currentSession.EnsureAuthenticated();

        if (currentSession.Role != UserRole.Admin)
        {
            throw new AppException(
                "forbidden",
                "Forbidden",
                403,
                "This action requires the Admin role.");
        }
    }

    public static void EnsureManagerOrAdmin(this ICurrentSession currentSession)
    {
        currentSession.EnsureAuthenticated();

        if (currentSession.Role is not (UserRole.Admin or UserRole.Manager))
        {
            throw new AppException(
                "forbidden",
                "Forbidden",
                403,
                "This action requires the Manager or Admin role.");
        }
    }
}

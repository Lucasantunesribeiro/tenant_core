namespace TenantCore.Application.Common.Abstractions;

public interface IPlanLimitService
{
    Task EnsureUserSlotAvailableAsync(CancellationToken cancellationToken);

    Task EnsureProjectSlotAvailableAsync(CancellationToken cancellationToken);

    Task EnsureClientSlotAvailableAsync(CancellationToken cancellationToken);
}

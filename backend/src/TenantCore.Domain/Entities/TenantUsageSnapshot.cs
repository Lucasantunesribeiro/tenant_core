using TenantCore.Domain.Common;

namespace TenantCore.Domain.Entities;

public sealed class TenantUsageSnapshot : TenantOwnedEntity
{
    private TenantUsageSnapshot()
    {
    }

    public TenantUsageSnapshot(
        Guid tenantId,
        DateTimeOffset capturedAtUtc,
        int activeUsers,
        int projectsCount,
        int tasksCount,
        int clientsCount)
    {
        TenantId = tenantId;
        CapturedAtUtc = capturedAtUtc;
        ActiveUsers = activeUsers;
        ProjectsCount = projectsCount;
        TasksCount = tasksCount;
        ClientsCount = clientsCount;
    }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public int ActiveUsers { get; private set; }

    public int ProjectsCount { get; private set; }

    public int TasksCount { get; private set; }

    public int ClientsCount { get; private set; }
}

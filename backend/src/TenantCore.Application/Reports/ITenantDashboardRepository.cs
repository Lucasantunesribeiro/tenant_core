namespace TenantCore.Application.Reports;

public interface ITenantDashboardRepository
{
    Task<TenantProjectDashboard> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantProjectDashboard(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    IReadOnlyList<ProjectDashboardRow> Projects);

public sealed record ProjectDashboardRow(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    string ProjectStatus,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? ClientName,
    string? OwnerFullName,
    int TotalTasks,
    int BacklogTasks,
    int InProgressTasks,
    int BlockedTasks,
    int DoneTasks,
    int OverdueTasks);

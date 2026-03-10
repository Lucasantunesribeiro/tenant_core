using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class WorkTask : TenantOwnedEntity
{
    private WorkTask()
    {
    }

    public WorkTask(
        Guid tenantId,
        Guid projectId,
        Guid? assigneeUserId,
        string title,
        string description,
        WorkTaskStatus status,
        TaskPriority priority,
        DateOnly? dueDate)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        AssigneeUserId = assigneeUserId;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        DueDate = dueDate;
    }

    public Guid ProjectId { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public WorkTaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public void Update(
        Guid projectId,
        Guid? assigneeUserId,
        string title,
        string description,
        WorkTaskStatus status,
        TaskPriority priority,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        ProjectId = projectId;
        AssigneeUserId = assigneeUserId;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        DueDate = dueDate;
        Touch(now);
    }

    public void UpdateStatus(WorkTaskStatus status, DateTimeOffset now)
    {
        Status = status;
        Touch(now);
    }
}

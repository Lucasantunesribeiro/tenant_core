using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class Project : TenantOwnedEntity
{
    private Project()
    {
    }

    public Project(
        Guid tenantId,
        Guid? clientId,
        Guid? ownerUserId,
        string name,
        string code,
        string description,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? dueDate)
    {
        TenantId = tenantId;
        ClientId = clientId;
        OwnerUserId = ownerUserId;
        Name = name;
        Code = code;
        Description = description;
        Status = status;
        StartDate = startDate;
        DueDate = dueDate;
    }

    public Guid? ClientId { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ProjectStatus Status { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public void Update(
        Guid? clientId,
        Guid? ownerUserId,
        string name,
        string code,
        string description,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        ClientId = clientId;
        OwnerUserId = ownerUserId;
        Name = name;
        Code = code;
        Description = description;
        Status = status;
        StartDate = startDate;
        DueDate = dueDate;
        Touch(now);
    }
}

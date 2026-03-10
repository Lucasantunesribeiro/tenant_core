using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class SubscriptionPlan : AuditableEntity
{
    private SubscriptionPlan()
    {
    }

    public SubscriptionPlan(
        PlanCode code,
        string name,
        string description,
        int maxUsers,
        int maxProjects,
        int maxClients)
    {
        Code = code;
        Name = name;
        Description = description;
        MaxUsers = maxUsers;
        MaxProjects = maxProjects;
        MaxClients = maxClients;
    }

    public PlanCode Code { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int MaxUsers { get; private set; }

    public int MaxProjects { get; private set; }

    public int MaxClients { get; private set; }
}

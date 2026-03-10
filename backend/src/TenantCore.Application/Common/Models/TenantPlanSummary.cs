using TenantCore.Domain.Enums;

namespace TenantCore.Application.Common.Models;

public sealed record TenantPlanSummary(
    PlanCode PlanCode,
    string PlanName,
    int MaxUsers,
    int MaxProjects,
    int MaxClients,
    QuotaState QuotaState,
    string? WarningMessage);

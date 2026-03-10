using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class TenantSubscription : TenantOwnedEntity
{
    private TenantSubscription()
    {
    }

    public TenantSubscription(
        Guid tenantId,
        PlanCode planCode,
        DateTimeOffset renewedAtUtc,
        DateTimeOffset nextRenewalAtUtc)
    {
        TenantId = tenantId;
        PlanCode = planCode;
        RenewedAtUtc = renewedAtUtc;
        NextRenewalAtUtc = nextRenewalAtUtc;
        Status = SubscriptionStatus.Active;
        QuotaState = QuotaState.Healthy;
    }

    public PlanCode PlanCode { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public QuotaState QuotaState { get; private set; }

    public DateTimeOffset RenewedAtUtc { get; private set; }

    public DateTimeOffset NextRenewalAtUtc { get; private set; }

    public DateTimeOffset? LastQuotaCheckAtUtc { get; private set; }

    public string? WarningMessage { get; private set; }

    public void ChangePlan(PlanCode planCode, DateTimeOffset now)
    {
        PlanCode = planCode;
        RenewedAtUtc = now;
        NextRenewalAtUtc = now.AddMonths(1);
        Status = SubscriptionStatus.Active;
        WarningMessage = null;
        Touch(now);
    }

    public void SetQuotaState(QuotaState quotaState, string? warningMessage, DateTimeOffset now)
    {
        QuotaState = quotaState;
        WarningMessage = warningMessage;
        LastQuotaCheckAtUtc = now;
        Status = quotaState == QuotaState.Exceeded ? SubscriptionStatus.Warning : SubscriptionStatus.Active;
        Touch(now);
    }
}

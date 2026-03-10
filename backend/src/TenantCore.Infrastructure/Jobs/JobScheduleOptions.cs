namespace TenantCore.Infrastructure.Jobs;

public sealed class JobScheduleOptions
{
    public const string SectionName = "Jobs";

    public string UsageSnapshotCron { get; init; } = "0 */10 * * * ?";

    public string SubscriptionEnforcementCron { get; init; } = "0 */15 * * * ?";

    public string CleanupCron { get; init; } = "0 0 2 * * ?";
}

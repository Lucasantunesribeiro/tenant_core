namespace TenantCore.Application.Common.Models;

public sealed record UsageOverview(
    int ActiveUsers,
    int Projects,
    int Tasks,
    int Clients,
    DateTimeOffset? LastSnapshotAtUtc);

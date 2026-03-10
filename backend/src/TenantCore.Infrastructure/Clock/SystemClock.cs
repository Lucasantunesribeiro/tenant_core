using TenantCore.Application.Common.Abstractions;

namespace TenantCore.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

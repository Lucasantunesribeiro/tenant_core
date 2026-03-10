using TenantCore.Application.Common.Abstractions;

namespace TenantCore.UnitTests.Common;

internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

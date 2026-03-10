using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TenantCore.Infrastructure.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis responded in {latency.TotalMilliseconds:0.##} ms.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }
}

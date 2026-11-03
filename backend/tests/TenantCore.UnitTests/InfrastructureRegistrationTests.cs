using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TenantCore.Infrastructure;

namespace TenantCore.UnitTests;

public sealed class InfrastructureRegistrationTests
{
    private static ServiceProvider Build(string? redis)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = "Server=localhost;Database=tenant_core;TrustServerCertificate=true",
                ["ConnectionStrings:Redis"] = redis,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static IEnumerable<string> HealthCheckNames(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(x => x.Name);

    [Fact]
    public void WithoutRedis_ShouldFallBackToInMemoryCacheAndSkipRedisHealthCheck()
    {
        using var provider = Build(redis: null);

        provider.GetRequiredService<IDistributedCache>().Should().BeOfType<MemoryDistributedCache>();
        provider.GetService<IConnectionMultiplexer>().Should().BeNull();
        HealthCheckNames(provider).Should().BeEquivalentTo("database");
    }

    [Fact]
    public void WithRedis_ShouldUseRedisCacheAndRedisHealthCheck()
    {
        using var provider = Build(redis: "localhost:6379");

        provider.GetRequiredService<IDistributedCache>().Should().BeAssignableTo<RedisCache>();
        HealthCheckNames(provider).Should().BeEquivalentTo("database", "redis");
    }
}

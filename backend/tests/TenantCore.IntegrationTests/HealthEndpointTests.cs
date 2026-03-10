using System.Net;
using FluentAssertions;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthEndpoints_ShouldReturnHealthyResponses()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();

        var liveResponse = await host.Client.GetAsync("/health/live");
        var readyResponse = await host.Client.GetAsync("/health/ready");

        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

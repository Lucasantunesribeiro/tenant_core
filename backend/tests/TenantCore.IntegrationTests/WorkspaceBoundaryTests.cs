using System.Net;
using FluentAssertions;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

public sealed class WorkspaceBoundaryTests
{
    [Fact]
    public async Task RegularUser_ShouldNotCreateClient()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("user@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var request = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/clients",
            session,
            new
            {
                name = "Blocked Client",
                email = "blocked@test.dev",
                contactName = "Blocked User",
                status = "Lead",
                notes = "should fail",
            });

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_ShouldCreateClientSuccessfully()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("manager@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var request = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/clients",
            session,
            new
            {
                name = "Managed Account",
                email = "managed@test.dev",
                contactName = "Jordan Scale",
                status = "Active",
                notes = "manager owned",
            });

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

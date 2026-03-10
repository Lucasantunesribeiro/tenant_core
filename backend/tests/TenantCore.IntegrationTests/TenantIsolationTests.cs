using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Application.Clients.Queries;
using TenantCore.Application.Common.Models;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task ClientQueries_ShouldOnlyReturnCurrentTenantData()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("admin@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var request = host.CreateAuthorizedRequest(HttpMethod.Get, "/api/clients?page=1&pageSize=20", session);
        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<ClientListItem>>(TenantCoreApiTestHost.JsonOptions);
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items.First().Name.Should().Be("Northwind Logistics");
        payload.Items.Should().NotContain(item => item.Name == "Blue Orbit Retail");
    }

    [Fact]
    public async Task TenantHeaderMismatch_ShouldReturnForbiddenProblemDetails()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("admin@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var request = host.CreateAuthorizedRequest(
            HttpMethod.Get,
            "/api/clients?page=1&pageSize=20",
            session,
            tenantId: TenantCoreApiTestHost.GlobexTenantId.ToString());

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await TenantCoreApiTestHost.ReadProblemAsync(response);
        problem.Type.Should().Be("tenant_mismatch");
    }
}

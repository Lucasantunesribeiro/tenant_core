using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Api.Controllers;
using TenantCore.Application.Common.Security;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

public sealed class AuthFlowTests
{
    [Fact]
    public async Task LoginRefreshAndLogout_ShouldRotateAndRevokeRefreshTokens()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "admin@acme.test",
                password = "Passw0rd!",
            }),
        };
        loginRequest.Headers.Add(HeaderNames.TenantId, TenantCoreApiTestHost.AcmeTenantId.ToString());

        var loginResponse = await host.Client.SendAsync(loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>(TenantCoreApiTestHost.JsonOptions);
        loginPayload.Should().NotBeNull();

        var refreshCookie = TenantCoreApiTestHost.ExtractRefreshCookie(loginResponse);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add(HeaderNames.TenantId, TenantCoreApiTestHost.AcmeTenantId.ToString());
        refreshRequest.Headers.Add("Cookie", refreshCookie);

        var refreshResponse = await host.Client.SendAsync(refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<RefreshEnvelope>(TenantCoreApiTestHost.JsonOptions);
        refreshPayload.Should().NotBeNull();
        refreshPayload!.AccessToken.Should().NotBe(loginPayload!.AccessToken);

        var rotatedCookie = TenantCoreApiTestHost.ExtractRefreshCookie(refreshResponse);

        var logoutRequest = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/logout",
            loginPayload,
            tenantId: TenantCoreApiTestHost.AcmeTenantId.ToString(),
            refreshCookie: rotatedCookie);

        var logoutResponse = await host.Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var revokedRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        revokedRefreshRequest.Headers.Add(HeaderNames.TenantId, TenantCoreApiTestHost.AcmeTenantId.ToString());
        revokedRefreshRequest.Headers.Add("Cookie", rotatedCookie);

        var revokedRefreshResponse = await host.Client.SendAsync(revokedRefreshRequest);
        revokedRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

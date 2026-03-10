using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Application.AuditLogs.Queries;
using TenantCore.Application.Common.Models;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

public sealed class PlanLimitAndAuditTests
{
    [Fact]
    public async Task CreatingUser_OnFreePlanAtCapacity_ShouldReturnPlanLimitError()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("admin@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var changePlanRequest = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/tenant/subscription/change-plan",
            session,
            new
            {
                planCode = "Free",
            });

        var changePlanResponse = await host.Client.SendAsync(changePlanRequest);
        changePlanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var createUserRequest = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/users",
            session,
            new
            {
                email = "new.user@acme.test",
                fullName = "New User",
                password = "Passw0rd!",
                role = "User",
                invitationPending = false,
            });

        var createUserResponse = await host.Client.SendAsync(createUserRequest);

        createUserResponse.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await TenantCoreApiTestHost.ReadProblemAsync(createUserResponse);
        problem.Type.Should().Be("plan_limit_exceeded");
    }

    [Fact]
    public async Task ClientCreate_ShouldWriteAuditLogEntry()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var session = await host.LoginAsync("admin@acme.test", TenantCoreApiTestHost.AcmeTenantId.ToString());

        var createClientRequest = host.CreateAuthorizedRequest(
            HttpMethod.Post,
            "/api/clients",
            session,
            new
            {
                name = "Audit Trail Co",
                email = "audit@client.dev",
                contactName = "Ava Audit",
                status = "Lead",
                notes = "audit me",
            });

        var createClientResponse = await host.Client.SendAsync(createClientRequest);
        createClientResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var auditRequest = host.CreateAuthorizedRequest(
            HttpMethod.Get,
            "/api/audit-logs?action=client.created&page=1&pageSize=50",
            session);

        var auditResponse = await host.Client.SendAsync(auditRequest);
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await auditResponse.Content.ReadFromJsonAsync<PagedResult<AuditLogItem>>(TenantCoreApiTestHost.JsonOptions);
        payload.Should().NotBeNull();
        payload!.Items.Should().Contain(entry => entry.Action == "client.created");
    }
}

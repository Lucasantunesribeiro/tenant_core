using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Api.Controllers;
using TenantCore.Application.AuditLogs.Queries;
using TenantCore.Application.Auth.Queries;
using TenantCore.Application.Clients.Queries;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Projects.Queries;
using TenantCore.Application.Tasks.Queries;
using TenantCore.Application.Tenants.Queries;
using TenantCore.Application.Users.Queries;
using TenantCore.Domain.Enums;
using TenantCore.IntegrationTests.Testing;

namespace TenantCore.IntegrationTests;

/// <summary>
/// Prova ponta a ponta (HTTP → middleware → handler → EF) que o tenant A (Acme) não lê,
/// altera nem apaga dados do tenant B (Globex), que recurso de outro tenant responde 404
/// (nunca 200/204/403 que confirmariam a existência do id) e que header forjado não troca tenant.
/// IDs fixos vêm do DemoDataSeeder.
/// </summary>
public sealed class CrossTenantAccessTests
{
    private static readonly string Acme = TenantCoreApiTestHost.AcmeTenantId.ToString();
    private static readonly string Globex = TenantCoreApiTestHost.GlobexTenantId.ToString();

    private static readonly Guid AcmeProjectId = Guid.Parse("d1111111-1111-1111-1111-111111111111");
    private static readonly Guid AcmeTaskId = Guid.Parse("e1111111-1111-1111-1111-111111111111");
    private static readonly Guid AcmeClientId = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid AcmeManagerId = Guid.Parse("aaaa2222-2222-2222-2222-222222222222");
    private static readonly Guid AcmeUserId = Guid.Parse("aaaa3333-3333-3333-3333-333333333333");

    private static readonly Guid GlobexProjectId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
    private static readonly Guid GlobexTaskId = Guid.Parse("e2222222-2222-2222-2222-222222222222");
    private static readonly Guid GlobexClientId = Guid.Parse("c2222222-2222-2222-2222-222222222222");
    private static readonly Guid GlobexAdminId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");

    private static object ProjectBody(Guid? clientId = null, Guid? ownerUserId = null, string name = "Hijacked", string code = "HIJACK") => new
    {
        clientId,
        ownerUserId,
        name,
        code,
        description = "cross-tenant attempt",
        status = "Active",
        startDate = (string?)null,
        dueDate = (string?)null,
    };

    private static object TaskBody(Guid projectId, Guid? assigneeUserId = null) => new
    {
        projectId,
        assigneeUserId,
        title = "Hijacked",
        description = "cross-tenant attempt",
        status = "Done",
        priority = "Critical",
        dueDate = (string?)null,
    };

    private static readonly object ClientBody = new
    {
        name = "Hijacked",
        email = "hijacked@evil.test",
        contactName = "Evil",
        status = "Inactive",
        notes = "cross-tenant attempt",
    };

    private static async Task<T> GetAsync<T>(TenantCoreApiTestHost host, LoginEnvelope session, string uri)
    {
        var response = await host.Client.SendAsync(host.CreateAuthorizedRequest(HttpMethod.Get, uri, session));
        response.StatusCode.Should().Be(HttpStatusCode.OK, "GET {0} deveria responder 200", uri);
        var payload = await response.Content.ReadFromJsonAsync<T>(TenantCoreApiTestHost.JsonOptions);
        return payload!;
    }

    private static async Task AssertNotFoundAsync(TenantCoreApiTestHost host, HttpRequestMessage request, string expectedType)
    {
        var response = await host.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} {1} deveria ser 404", request.Method, request.RequestUri);
        (await TenantCoreApiTestHost.ReadProblemAsync(response)).Type.Should().Be(expectedType);
    }

    private static async Task AssertGlobexWorkspaceIntactAsync(TenantCoreApiTestHost host)
    {
        var globex = await host.LoginAsync("admin@globex.test", Globex);

        var projects = await GetAsync<PagedResult<ProjectListItem>>(host, globex, "/api/projects?pageSize=100");
        projects.Items.Should().ContainSingle(x => x.Id == GlobexProjectId)
            .Which.Name.Should().Be("Advisory Portal Refresh");

        var tasks = await GetAsync<PagedResult<TaskListItem>>(host, globex, "/api/tasks?pageSize=100");
        var task = tasks.Items.Should().ContainSingle(x => x.Id == GlobexTaskId).Subject;
        task.Title.Should().Be("Review workspace navigation");
        task.Status.Should().Be(WorkTaskStatus.Backlog);

        var clients = await GetAsync<PagedResult<ClientListItem>>(host, globex, "/api/clients?pageSize=100");
        clients.Items.Should().ContainSingle(x => x.Id == GlobexClientId)
            .Which.Name.Should().Be("Blue Orbit Retail");

        var me = await GetAsync<CurrentUserResponse>(host, globex, "/api/auth/me");
        me.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task AcmeAdmin_ShouldGet404_WhenUpdatingOrDeletingGlobexResourcesById()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/projects/{GlobexProjectId}", acme, ProjectBody()), "project_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Delete, $"/api/projects/{GlobexProjectId}", acme), "project_not_found");

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/tasks/{GlobexTaskId}", acme, TaskBody(AcmeProjectId)), "task_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Patch, $"/api/tasks/{GlobexTaskId}/status", acme, new { status = "Done" }), "task_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Delete, $"/api/tasks/{GlobexTaskId}", acme), "task_not_found");

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/clients/{GlobexClientId}", acme, ClientBody), "client_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Delete, $"/api/clients/{GlobexClientId}", acme), "client_not_found");

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Patch, $"/api/users/{GlobexAdminId}/role", acme, new { role = "User" }), "user_not_found");

        await AssertGlobexWorkspaceIntactAsync(host);
    }

    [Fact]
    public async Task AcmeManager_ShouldNotMoveOwnTaskIntoGlobexProjectOrAssignGlobexUser()
    {
        // Regressão: UpdateTask aceitava qualquer ProjectId/AssigneeUserId do corpo.
        // O FK aceitava o id de outro tenant; apagar o projeto Globex apagaria a tarefa Acme em cascata.
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("manager@acme.test", Acme);

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/tasks/{AcmeTaskId}", acme, TaskBody(GlobexProjectId, AcmeUserId)), "project_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/tasks/{AcmeTaskId}", acme, TaskBody(AcmeProjectId, GlobexAdminId)), "assignee_not_found");

        var tasks = await GetAsync<PagedResult<TaskListItem>>(host, acme, "/api/tasks?pageSize=100");
        var task = tasks.Items.Should().ContainSingle(x => x.Id == AcmeTaskId).Subject;
        task.ProjectId.Should().Be(AcmeProjectId);
        task.AssigneeUserId.Should().Be(AcmeUserId);
        task.Title.Should().Be("Wire audit dashboard");
    }

    [Fact]
    public async Task AcmeManager_ShouldNotLinkOwnProjectToGlobexClientOrOwner()
    {
        // Regressão: UpdateProject aceitava ClientId/OwnerUserId de outro tenant.
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("manager@acme.test", Acme);

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/projects/{AcmeProjectId}", acme, ProjectBody(clientId: GlobexClientId, code: "TCORE")), "client_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Put, $"/api/projects/{AcmeProjectId}", acme, ProjectBody(ownerUserId: GlobexAdminId, code: "TCORE")), "owner_not_found");

        var projects = await GetAsync<PagedResult<ProjectListItem>>(host, acme, "/api/projects?pageSize=100");
        var project = projects.Items.Should().ContainSingle(x => x.Id == AcmeProjectId).Subject;
        project.ClientId.Should().Be(AcmeClientId);
        project.OwnerUserId.Should().Be(AcmeManagerId);
        project.Name.Should().Be("Tenant Core Rollout");
    }

    [Fact]
    public async Task AcmeManager_ShouldStillUpdateOwnProjectAndTaskWithOwnReferences()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("manager@acme.test", Acme);

        var projectResponse = await host.Client.SendAsync(host.CreateAuthorizedRequest(
            HttpMethod.Put, $"/api/projects/{AcmeProjectId}", acme,
            ProjectBody(clientId: AcmeClientId, ownerUserId: AcmeManagerId, name: "Renamed", code: "TCORE")));
        projectResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var taskResponse = await host.Client.SendAsync(host.CreateAuthorizedRequest(
            HttpMethod.Put, $"/api/tasks/{AcmeTaskId}", acme, TaskBody(AcmeProjectId, AcmeUserId)));
        taskResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcmeManager_ShouldNotCreateTaskInGlobexProject()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("manager@acme.test", Acme);

        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Post, "/api/tasks", acme, TaskBody(GlobexProjectId)), "project_not_found");
        await AssertNotFoundAsync(host, host.CreateAuthorizedRequest(HttpMethod.Post, "/api/projects", acme, ProjectBody(clientId: GlobexClientId, code: "NEWCODE")), "client_not_found");
    }

    [Theory]
    [InlineData("DELETE", "/api/projects/d2222222-2222-2222-2222-222222222222")]
    [InlineData("DELETE", "/api/clients/c2222222-2222-2222-2222-222222222222")]
    [InlineData("DELETE", "/api/tasks/e2222222-2222-2222-2222-222222222222")]
    [InlineData("GET", "/api/projects")]
    [InlineData("GET", "/api/tenant/profile")]
    [InlineData("GET", "/api/tenant/usage")]
    [InlineData("GET", "/api/audit-logs")]
    public async Task ForgedTenantHeader_ShouldBeRejectedWith403AndNotSwitchTenant(string method, string uri)
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);

        var response = await host.Client.SendAsync(host.CreateAuthorizedRequest(new HttpMethod(method), uri, acme, tenantId: Globex));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await TenantCoreApiTestHost.ReadProblemAsync(response)).Type.Should().Be("tenant_mismatch");

        await AssertGlobexWorkspaceIntactAsync(host);
    }

    [Fact]
    public async Task DuplicatedTenantHeader_ShouldNotBeAcceptedAsTenantSwitch()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);

        var request = host.CreateAuthorizedRequest(HttpMethod.Get, "/api/projects", acme, tenantId: Acme);
        request.Headers.Add(HeaderNames.TenantId, Globex);

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TenantIdInRequestBody_ShouldBeIgnored_EntityIsStampedWithTokenTenant()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);

        var create = await host.Client.SendAsync(host.CreateAuthorizedRequest(HttpMethod.Post, "/api/clients", acme, new
        {
            tenantId = Globex,
            name = "Smuggled Client",
            email = "smuggled@test.dev",
            contactName = "Smuggler",
            status = "Active",
            notes = "body carries foreign tenantId",
        }));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var acmeClients = await GetAsync<PagedResult<ClientListItem>>(host, acme, "/api/clients?pageSize=100");
        acmeClients.Items.Should().Contain(x => x.Name == "Smuggled Client");

        var globex = await host.LoginAsync("admin@globex.test", Globex);
        var globexClients = await GetAsync<PagedResult<ClientListItem>>(host, globex, "/api/clients?pageSize=100");
        globexClients.Items.Should().NotContain(x => x.Name == "Smuggled Client");
    }

    [Fact]
    public async Task ListEndpoints_ShouldNeverReturnRowsOfAnotherTenant()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);

        var projects = await GetAsync<PagedResult<ProjectListItem>>(host, acme, "/api/projects?pageSize=100");
        projects.Items.Should().NotBeEmpty().And.NotContain(x => x.Id == GlobexProjectId);

        var tasks = await GetAsync<PagedResult<TaskListItem>>(host, acme, "/api/tasks?pageSize=100");
        tasks.Items.Should().NotBeEmpty().And.NotContain(x => x.Id == GlobexTaskId);

        var tasksFilteredByGlobexProject = await GetAsync<PagedResult<TaskListItem>>(host, acme, $"/api/tasks?projectId={GlobexProjectId}");
        tasksFilteredByGlobexProject.Items.Should().BeEmpty();
        tasksFilteredByGlobexProject.TotalCount.Should().Be(0);

        var users = await GetAsync<PagedResult<UserListItem>>(host, acme, "/api/users?pageSize=100");
        users.Items.Should().NotBeEmpty().And.NotContain(x => x.Id == GlobexAdminId);
        users.Items.Should().OnlyContain(x => x.Email.EndsWith("@acme.test"));

        var audit = await GetAsync<PagedResult<AuditLogItem>>(host, acme, "/api/audit-logs?pageSize=100");
        audit.Items.Should().NotBeEmpty().And.NotContain(x => x.CorrelationId == "seed-globex");

        var profile = await GetAsync<TenantProfileResponse>(host, acme, "/api/tenant/profile");
        profile.Id.Should().Be(TenantCoreApiTestHost.AcmeTenantId);
    }

    [Fact]
    public async Task CachedTenantResponses_ShouldBeKeyedPerTenant()
    {
        // O cache de teste é singleton e compartilhado: se a chave não tivesse o tenant,
        // a segunda leitura devolveria o plano do primeiro tenant.
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acme = await host.LoginAsync("admin@acme.test", Acme);
        var globex = await host.LoginAsync("admin@globex.test", Globex);

        var acmeSubscription = await GetAsync<SubscriptionResponse>(host, acme, "/api/tenant/subscription");
        var globexSubscription = await GetAsync<SubscriptionResponse>(host, globex, "/api/tenant/subscription");
        acmeSubscription.PlanCode.Should().Be(PlanCode.Business);
        globexSubscription.PlanCode.Should().Be(PlanCode.Pro);

        var acmeUsage = await GetAsync<UsageDashboardResponse>(host, acme, "/api/tenant/usage");
        var globexUsage = await GetAsync<UsageDashboardResponse>(host, globex, "/api/tenant/usage");
        acmeUsage.Plan.PlanCode.Should().Be(PlanCode.Business);
        globexUsage.Plan.PlanCode.Should().Be(PlanCode.Pro);
        acmeUsage.Usage.ActiveUsers.Should().Be(3);
        globexUsage.Usage.ActiveUsers.Should().Be(1);
    }

    [Fact]
    public async Task Login_WithCredentialsOfAnotherTenant_ShouldReturn401()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@acme.test", password = "Passw0rd!" }),
        };
        request.Headers.Add(HeaderNames.TenantId, Globex);

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TenantCoreApiTestHost.ReadProblemAsync(response)).Type.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Refresh_WithAcmeCookieAndGlobexHeader_ShouldReturn401()
    {
        await using var host = await TenantCoreApiTestHost.CreateAsync();
        var acmeCookie = await host.LoginAndGetRefreshCookieAsync("admin@acme.test", Acme);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add(HeaderNames.TenantId, Globex);
        request.Headers.Add("Cookie", acmeCookie);

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TenantCoreApiTestHost.ReadProblemAsync(response)).Type.Should().Be("invalid_refresh_token");
    }
}

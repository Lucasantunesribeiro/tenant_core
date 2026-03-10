using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Projects.Commands;
using TenantCore.Application.Projects.Queries;
using TenantCore.Domain.Enums;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/projects")]
public sealed class ProjectsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectListItem>>> GetProjects(
        [FromQuery] string? search,
        [FromQuery] ProjectStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Sender.Send(new GetProjectsQuery(search, status, page, pageSize), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateProject([FromBody] ProjectRequest request, CancellationToken cancellationToken)
    {
        var projectId = await Sender.Send(
            new CreateProjectCommand(
                request.ClientId,
                request.OwnerUserId,
                request.Name,
                request.Code,
                request.Description,
                request.Status,
                request.StartDate,
                request.DueDate),
            cancellationToken);

        return CreatedAtAction(nameof(GetProjects), new { id = projectId }, projectId);
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] ProjectRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpdateProjectCommand(
                projectId,
                request.ClientId,
                request.OwnerUserId,
                request.Name,
                request.Code,
                request.Description,
                request.Status,
                request.StartDate,
                request.DueDate),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> DeleteProject(Guid projectId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteProjectCommand(projectId), cancellationToken);
        return NoContent();
    }
}

public sealed record ProjectRequest(
    Guid? ClientId,
    Guid? OwnerUserId,
    string Name,
    string Code,
    string Description,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? DueDate);

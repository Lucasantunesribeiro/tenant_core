using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Tasks.Commands;
using TenantCore.Application.Tasks.Queries;
using TenantCore.Domain.Enums;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/tasks")]
public sealed class TasksController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskListItem>>> GetTasks(
        [FromQuery] string? search,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] WorkTaskStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Sender.Send(new GetTasksQuery(search, projectId, assigneeUserId, status, page, pageSize), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateTask([FromBody] TaskRequest request, CancellationToken cancellationToken)
    {
        var taskId = await Sender.Send(
            new CreateTaskCommand(
                request.ProjectId,
                request.AssigneeUserId,
                request.Title,
                request.Description,
                request.Status,
                request.Priority,
                request.DueDate),
            cancellationToken);

        return CreatedAtAction(nameof(GetTasks), new { id = taskId }, taskId);
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] TaskRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpdateTaskCommand(
                taskId,
                request.ProjectId,
                request.AssigneeUserId,
                request.Title,
                request.Description,
                request.Status,
                request.Priority,
                request.DueDate),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPatch("{taskId:guid}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid taskId, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateTaskStatusCommand(taskId, request.Status), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteTaskCommand(taskId), cancellationToken);
        return NoContent();
    }
}

public sealed record TaskRequest(
    Guid ProjectId,
    Guid? AssigneeUserId,
    string Title,
    string Description,
    WorkTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate);

public sealed record UpdateTaskStatusRequest(WorkTaskStatus Status);

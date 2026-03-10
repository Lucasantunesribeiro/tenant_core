using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tasks.Commands;

public sealed record CreateTaskCommand(
    Guid ProjectId,
    Guid? AssigneeUserId,
    string Title,
    string Description,
    WorkTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate) : IRequest<Guid>;

internal sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(600);
    }
}

internal sealed class CreateTaskCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var projectExists = await dbContext.Projects.AnyAsync(x => x.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            throw new AppException("project_not_found", "Project not found", 404, "The selected project does not exist.");
        }

        if (request.AssigneeUserId.HasValue)
        {
            var assigneeExists = await dbContext.Users.AnyAsync(x => x.Id == request.AssigneeUserId.Value, cancellationToken);
            if (!assigneeExists)
            {
                throw new AppException("assignee_not_found", "Assignee not found", 404, "The selected assignee does not exist.");
            }
        }

        var task = new WorkTask(
            currentSession.GetRequiredTenantId(),
            request.ProjectId,
            request.AssigneeUserId,
            request.Title.Trim(),
            request.Description.Trim(),
            request.Status,
            request.Priority,
            request.DueDate);

        await dbContext.Tasks.AddAsync(task, cancellationToken);
        await auditService.WriteAsync("task.created", "Task", task.Id.ToString(), request, cancellationToken);
        await cacheService.RemoveAsync($"usage:{task.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return task.Id;
    }
}
